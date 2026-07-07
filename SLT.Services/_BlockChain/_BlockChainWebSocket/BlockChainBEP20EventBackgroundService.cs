using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Util;
using Nethereum.Web3;
using SLT.Services._BlockChain._BlockChainWebSocket.DTOs;
using SLT.Services._BlockChain.DTOs.Settings;
using SLT.Services._TransactionLog;
using System.Numerics;
using System.Reactive.Linq;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._BlockChain._BlockChainWebSocket
{
    public class BlockChainBEP20EventBackgroundService : BackgroundService, IHostedDependency
    {
        #region Constants

        private const string InvoiceLogPrefix = "[BEP20-INVOICE-WS]";
        private const string StakeLogPrefix = "[BEP20-STAKE-WS]";
        private const string CommonPrefix = "[BEP20-WS]";
        private const string NetworkName = "BEP20";

        #endregion

        #region Services

        private readonly BlockChainSettings _blockChainSettings;
        private readonly BlockchainWebSocketSetting _settings;
        private readonly ITransactionLogService _transactionLogService;
        private readonly ILogger<BlockChainBEP20EventBackgroundService> _logger;

        #endregion

        #region Blockchain

        private Web3 _web3;
        private StreamingWebSocketClient _webSocketClient;

        private readonly string[] _rpcUrls;
        private readonly string[] _wsUrls;

        private int _currentRpcIndex = 0;
        private int _currentWsIndex = 0;

        private readonly string _invoiceContractAddress;
        private readonly string _stakeContractAddress;

        #endregion

        #region Blocks

        private BigInteger _invoiceLastProcessedBlock = 0;
        private BigInteger _stakeLastProcessedBlock = 0;

        #endregion

        #region Locks

        private readonly object _blockLock = new();

        private readonly SemaphoreSlim _reconnectLock = new(1, 1);
        private readonly SemaphoreSlim _cleanupLock = new(1, 1);

        #endregion

        #region Runtime

        private IDisposable _contractEventsSubscription;
        private IDisposable _stakeContractEventsSubscription;

        private int _reconnectAttempts = 0;
        private DateTime _lastEventReceived = DateTime.UtcNow;

        private bool _isDisposed = false;

        #endregion

        public BlockChainBEP20EventBackgroundService(
            BlockChainSettings blockChainSettings,
            ITransactionLogService transactionLogService,
            ILogger<BlockChainBEP20EventBackgroundService> logger,
            BlockchainWebSocketSetting settings)
        {
            _blockChainSettings = blockChainSettings;
            _transactionLogService = transactionLogService;
            _logger = logger;
            _settings = settings;

            _rpcUrls = new[]
            {
                _blockChainSettings.RpcUrl,
                _blockChainSettings.RpcUrl2
            };

            _wsUrls = new[]
            {
                _settings.WsUrl,
                _settings.WsUrl2
            };

            _invoiceContractAddress = _settings.ContractAddress;
            _stakeContractAddress = _blockChainSettings.BEP20StakeContractAddress;

            InitializeClients();
        }

        #region Initialize

        private void InitializeClients()
        {
            var rpcUrl = GetCurrentRpcUrl();

            _web3 = new Web3(rpcUrl);

            //_logger.LogInformation(
            //    "{Prefix} Web3 initialized with RPC: {RpcUrl}",
            //    CommonPrefix,
            //    rpcUrl
            //);
        }

        private string GetCurrentRpcUrl()
        {
            return _rpcUrls[_currentRpcIndex];
        }

        private string GetCurrentWsUrl()
        {
            return _wsUrls[_currentWsIndex];
        }

        private void SwitchRpc()
        {
            _currentRpcIndex = (_currentRpcIndex + 1) % _rpcUrls.Length;

            //_logger.LogWarning(
            //    "{Prefix} Switching RPC to: {Rpc}",
            //    CommonPrefix,
            //    GetCurrentRpcUrl()
            //);
        }

        private void SwitchWs()
        {
            _currentWsIndex = (_currentWsIndex + 1) % _wsUrls.Length;

            //_logger.LogWarning(
            //    "{Prefix} Switching WS to: {Ws}",
            //    CommonPrefix,
            //    GetCurrentWsUrl()
            //);
        }

        #endregion



        #region Execute

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _invoiceLastProcessedBlock = await GetInvoiceLastProcessedBlock(stoppingToken);
            //_logger.LogInformation("{Prefix} Invoice starting block : {Block}", InvoiceLogPrefix, _invoiceLastProcessedBlock);

            _stakeLastProcessedBlock = await GetStakeLastProcessedBlock(stoppingToken);
            //_logger.LogInformation("{Prefix} Stake starting block : {Block}", StakeLogPrefix, _stakeLastProcessedBlock);


            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TryConnectWithRetryAsync(stoppingToken);

                    _lastEventReceived = DateTime.UtcNow;

                    while (_webSocketClient?.IsStarted == true && !stoppingToken.IsCancellationRequested)
                    {
                        var now = DateTime.UtcNow;

                        if ((now - _lastEventReceived).TotalMinutes > 2)
                        {
                            _logger.LogWarning(
                                "{Prefix} Heartbeat timeout detected. Reconnecting...",
                                CommonPrefix
                            );

                            await Task.Delay(2000, stoppingToken);

                            await TryConnectWithRetryAsync(stoppingToken);

                            _lastEventReceived = DateTime.UtcNow;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }

                    if (_webSocketClient != null && !_webSocketClient.IsStarted)
                    {
                        await Task.Delay(2000, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Prefix} Unexpected execute error", CommonPrefix);

                    SwitchRpc();
                    SwitchWs();
                    InitializeClients();

                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        #endregion



        #region Connection

        private async Task TryConnectWithRetryAsync(CancellationToken stoppingToken)
        {
            if (!await _reconnectLock.WaitAsync(0, stoppingToken))
            {
                return;
            }

            try
            {
                _reconnectAttempts = 0;

                while (!stoppingToken.IsCancellationRequested && _reconnectAttempts < _settings.MaxReconnectAttempts)
                {
                    try
                    {
                        //_logger.LogInformation(
                        //    "{Prefix} Connecting attempt {Attempt}",
                        //    CommonPrefix,
                        //    _reconnectAttempts + 1);

                        await ConnectAndSubscribe(stoppingToken);
                        _reconnectAttempts = 0;
                        return;
                    }
                    catch (Exception ex)
                    {
                        _reconnectAttempts++;

                        _logger.LogWarning(ex, "{Prefix} Connection failed. Retry: {Retry}", CommonPrefix, _reconnectAttempts);

                        SwitchRpc();
                        SwitchWs();
                        InitializeClients();

                        await Task.Delay(CalculateReconnectDelay(), stoppingToken);
                    }
                }

                if (_reconnectAttempts >= _settings.MaxReconnectAttempts)
                {
                    //_logger.LogCritical(
                    //    "{Prefix} Max reconnect reached",
                    //    CommonPrefix);

                    await Task.Delay(30000, stoppingToken);

                    _reconnectAttempts = 0;
                }
            }
            finally
            {
                _reconnectLock.Release();
            }
        }

        private TimeSpan CalculateReconnectDelay()
        {
            double delaySeconds = Math.Min(Math.Pow(2, _reconnectAttempts) * _settings.ReconnectInterval, 300);
            return TimeSpan.FromSeconds(delaySeconds);
        }

        private async Task ConnectAndSubscribe(CancellationToken cancellationToken)
        {
            await CleanupConnection();

            var currentWsUrl = GetCurrentWsUrl();
            _webSocketClient = new StreamingWebSocketClient(currentWsUrl);
            _web3 = new Web3(GetCurrentRpcUrl());

            try
            {
                await _webSocketClient.StartAsync();

                await SubscribeToInvoiceContractEventsAsync(cancellationToken);

                await SubscribeToStakeContractEventsAsync(cancellationToken);

                _logger.LogInformation("{Prefix} Subscriptions are active", CommonPrefix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} ConnectAndSubscribe failed", CommonPrefix);
                throw;
            }
        }

        private async Task CleanupConnection()
        {
            if (!await _cleanupLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                _contractEventsSubscription?.Dispose();
                _contractEventsSubscription = null;

                _stakeContractEventsSubscription?.Dispose();
                _stakeContractEventsSubscription = null;

                if (_webSocketClient != null)
                {
                    try
                    {
                        if (_webSocketClient.IsStarted)
                        {
                            await _webSocketClient.StopAsync();

                            await Task.Delay(300);
                        }
                    }
                    catch (Exception ex)
                    {
                        //_logger.LogWarning(
                        //    ex,
                        //    "{Prefix} StopAsync failed",
                        //    CommonPrefix);
                    }

                    try
                    {
                        _webSocketClient.Dispose();
                    }
                    catch (Exception ex)
                    {
                        //_logger.LogWarning(
                        //    ex,
                        //    "{Prefix} Dispose failed",
                        //    CommonPrefix);
                    }

                    _webSocketClient = null;
                }
            }
            finally
            {
                _cleanupLock.Release();
            }
        }

        #endregion



        #region Invoice

        private async Task SubscribeToInvoiceContractEventsAsync(CancellationToken cancellationToken)
        {
            var subscription = new EthLogsObservableSubscription(_webSocketClient);

            var safeObservable = subscription.GetSubscriptionDataResponsesAsObservable()
                    .Where(log => log.Address.IsTheSameAddress(_invoiceContractAddress))
                    .Select(log => Observable.FromAsync(() => ProcessContractEventLogAsync(log, cancellationToken)))
                    .Concat();

            _contractEventsSubscription = safeObservable.Subscribe(_ => { },
                ex =>
                {
                    _logger.LogError(ex, "{Prefix} Invoice subscription error", InvoiceLogPrefix);
                },
                () =>
                {
                    _logger.LogWarning("{Prefix} Invoice subscription completed", InvoiceLogPrefix);
                });

            var filter = new NewFilterInput
            {
                Address = new[] { _invoiceContractAddress },
                FromBlock = new BlockParameter(await GetInvoiceLastProcessedBlock(cancellationToken))
            };

            await subscription.SubscribeAsync(filter);

            _logger.LogInformation("{Prefix} Invoice subscription active: {Address}", InvoiceLogPrefix, _invoiceContractAddress);
        }

        private async Task ProcessContractEventLogAsync(FilterLog log, CancellationToken cancellationToken)
        {
            try
            {
                var created = log.DecodeEvent<InvoiceCreatedEventDTO>();

                if (created != null)
                {
                    await CreateInvoiceCreatedLog(log, created, cancellationToken);
                    return;
                }

                var paid = log.DecodeEvent<InvoicePaidEventDTO>();

                if (paid != null)
                {
                    await CreateInvoicePaidLogAsync(log, paid, cancellationToken);
                    return;
                }
                var lockedCreated = log.DecodeEvent<LockedInvoiceCreatedEventDTO>();

                if (lockedCreated != null)
                {
                    await CreateLockedInvoiceCreatedLogAsync(log, lockedCreated, cancellationToken);
                    return;
                }

                var lockedPaid = log.DecodeEvent<LockedInvoicePaidEventDTO>();

                if (lockedPaid != null)
                {
                    await CreateLockedInvoicePaidLogAsync(log, lockedPaid, cancellationToken);
                    return;
                }

                var lockedApproved = log.DecodeEvent<LockedInvoiceApprovedEventDTO>();

                if (lockedApproved != null)
                {
                    await CreateLockedInvoiceApprovedLogAsync(log, lockedApproved, cancellationToken);
                    return;
                }

                var lockedResolved = log.DecodeEvent<LockedInvoiceResolvedEventDTO>();

                if (lockedResolved != null)
                {
                    await CreateLockedInvoiceResolvedLogAsync(log, lockedResolved, cancellationToken);
                    return;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Error decoding invoice log", InvoiceLogPrefix);
            }
        }

        private async Task CreateInvoiceCreatedLog(FilterLog log, EventLog<InvoiceCreatedEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);

                _logger.LogInformation("{Prefix} InvoiceCreated | InvoiceId: {InvoiceId}", InvoiceLogPrefix, invoiceId);

                var unlockDate = ConvertUnixSecondsToDateTime(eLog.Event.UnlockTime);

                await _transactionLogService.CreateInvoiceCreatedAsync(
                    new _TransactionLog.DTOs.InvoiceCreatedLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,
                        InvoiceId = invoiceId,
                        Creator = eLog.Event.Creator,
                        Token = eLog.Event.Token,
                        UsdAmount = Web3.Convert.FromWei(eLog.Event.UsdAmount),
                        UnLockTime = unlockDate,
                        EventType = Domain.Collections.BlockchainEventType.InvoiceCreated,
                        Network = NetworkName
                    });

                _lastEventReceived = DateTime.UtcNow;

                lock (_blockLock)
                {
                    _invoiceLastProcessedBlock = BigInteger.Max(_invoiceLastProcessedBlock, log.BlockNumber.Value + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} InvoiceCreated failed", InvoiceLogPrefix);

                throw;
            }
        }

        private async Task CreateInvoicePaidLogAsync(FilterLog log, EventLog<InvoicePaidEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);

                await _transactionLogService.CreateInvoicePaidAsync(
                    new _TransactionLog.DTOs.InvoicePaidLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,

                        InvoiceId = invoiceId,
                        Payer = eLog.Event.Payer,
                        Token = eLog.Event.Token,

                        PayAmount = Web3.Convert.FromWei(eLog.Event.PayAmount),
                        EventType = Domain.Collections.BlockchainEventType.InvoicePaid,
                        Network = NetworkName
                    });

                _lastEventReceived = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} InvoicePaid failed", InvoiceLogPrefix);
                throw;
            }
        }

        private async Task CreateLockedInvoiceCreatedLogAsync(FilterLog log, EventLog<LockedInvoiceCreatedEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureInvoiceEventCursorIncludesLockedEventsAsync();

                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);
                var unlockDate = ConvertUnixSecondsToDateTime(eLog.Event.UnlockTime);

                _logger.LogInformation(
                    "{Prefix} LockedInvoiceCreated received | InvoiceId: {InvoiceId}, Creator: {Creator}, Token: {Token}, UsdAmount: {UsdAmount}, LockDuration: {LockDuration}, Approver: {Approver}, UnlockDate: {UnlockDate}, Block: {Block}, Hash: {Hash}",
                    InvoiceLogPrefix, invoiceId, eLog.Event.Creator, eLog.Event.Token, eLog.Event.UsdAmount, eLog.Event.LockDuration, eLog.Event.Approver, unlockDate, log.BlockNumber, log.TransactionHash);

                await _transactionLogService.CreateLockedInvoiceCreatedAsync(
                    new _TransactionLog.DTOs.LockedInvoiceCreatedLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,
                        InvoiceId = invoiceId,
                        Creator = eLog.Event.Creator,
                        Token = eLog.Event.Token,
                        UsdAmount = eLog.Event.UsdAmount,
                        UnLockTime = unlockDate,
                        LockDuration = eLog.Event.LockDuration,
                        Approver = eLog.Event.Approver,
                        EventType = Domain.Collections.BlockchainEventType.LockedInvoiceCreated,
                        Network = NetworkName
                    });

                _lastEventReceived = DateTime.UtcNow;

                lock (_blockLock)
                {
                    _invoiceLastProcessedBlock = BigInteger.Max(_invoiceLastProcessedBlock, log.BlockNumber.Value + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} LockedInvoiceCreated failed", InvoiceLogPrefix);
                throw;
            }
        }

        private async Task CreateLockedInvoicePaidLogAsync(FilterLog log, EventLog<LockedInvoicePaidEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureInvoiceEventCursorIncludesLockedEventsAsync();

                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);
                var lockedUntil = ConvertUnixSecondsToDateTime(eLog.Event.LockedUntil);

                _logger.LogInformation(
                    "{Prefix} LockedInvoicePaid received | InvoiceId: {InvoiceId}, Payer: {Payer}, Token: {Token}, PayAmount: {PayAmount}, LockedUntil: {LockedUntil}, Block: {Block}, Hash: {Hash}",
                    InvoiceLogPrefix, invoiceId, eLog.Event.Payer, eLog.Event.Token, eLog.Event.PayAmount, lockedUntil, log.BlockNumber, log.TransactionHash);

                await _transactionLogService.CreateLockedInvoicePaidAsync(
                    new _TransactionLog.DTOs.LockedInvoicePaidLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,
                        InvoiceId = invoiceId,
                        Payer = eLog.Event.Payer,
                        Token = eLog.Event.Token,
                        PayAmount = eLog.Event.PayAmount,
                        LockedUntil = lockedUntil,
                        EventType = Domain.Collections.BlockchainEventType.LockedInvoicePaid,
                        Network = NetworkName
                    });

                _lastEventReceived = DateTime.UtcNow;

                lock (_blockLock)
                {
                    _invoiceLastProcessedBlock = BigInteger.Max(_invoiceLastProcessedBlock, log.BlockNumber.Value + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} LockedInvoicePaid failed", InvoiceLogPrefix);
                throw;
            }
        }

        private async Task CreateLockedInvoiceApprovedLogAsync(FilterLog log, EventLog<LockedInvoiceApprovedEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureInvoiceEventCursorIncludesLockedEventsAsync();

                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);

                _logger.LogInformation(
                    "{Prefix} LockedInvoiceApproved received | InvoiceId: {InvoiceId}, Approver: {Approver}, Block: {Block}, Hash: {Hash}",
                    InvoiceLogPrefix, invoiceId, eLog.Event.Approver, log.BlockNumber, log.TransactionHash);

                await _transactionLogService.CreateLockedInvoiceApprovedAsync(
                    new _TransactionLog.DTOs.LockedInvoiceApprovedLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,
                        InvoiceId = invoiceId,
                        Approver = eLog.Event.Approver,
                        EventType = Domain.Collections.BlockchainEventType.LockedInvoiceApproved,
                        Network = NetworkName
                    });

                _lastEventReceived = DateTime.UtcNow;

                lock (_blockLock)
                {
                    _invoiceLastProcessedBlock = BigInteger.Max(_invoiceLastProcessedBlock, log.BlockNumber.Value + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} LockedInvoiceApproved failed", InvoiceLogPrefix);
                throw;
            }
        }

        private async Task CreateLockedInvoiceResolvedLogAsync(FilterLog log, EventLog<LockedInvoiceResolvedEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureInvoiceEventCursorIncludesLockedEventsAsync();

                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);

                _logger.LogInformation(
                    "{Prefix} LockedInvoiceResolved received | InvoiceId: {InvoiceId}, Beneficiary: {Beneficiary}, Amount: {Amount}, FeeAmount: {FeeAmount}, Block: {Block}, Hash: {Hash}",
                    InvoiceLogPrefix, invoiceId, eLog.Event.Beneficiary, eLog.Event.Amount, eLog.Event.FeeAmount, log.BlockNumber, log.TransactionHash);

                await _transactionLogService.CreateLockedInvoiceResolvedAsync(
                    new _TransactionLog.DTOs.LockedInvoiceResolvedLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,
                        InvoiceId = invoiceId,
                        Beneficiary = eLog.Event.Beneficiary,
                        Amount = eLog.Event.Amount,
                        FeeAmount = eLog.Event.FeeAmount,
                        EventType = Domain.Collections.BlockchainEventType.LockedInvoiceResolved,
                        Network = NetworkName
                    });

                _lastEventReceived = DateTime.UtcNow;

                lock (_blockLock)
                {
                    _invoiceLastProcessedBlock = BigInteger.Max(_invoiceLastProcessedBlock, log.BlockNumber.Value + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} LockedInvoiceResolved failed", InvoiceLogPrefix);
                throw;
            }
        }

        private async Task<HexBigInteger> GetInvoiceLastProcessedBlock(CancellationToken cancellationToken)
        {
            try
            {
                lock (_blockLock)
                {
                    if (_invoiceLastProcessedBlock > 0)
                    {
                        return _invoiceLastProcessedBlock.ToHexBigInteger();
                    }
                }

                var lastDbBlock = await _transactionLogService.GetInvoiceLastCheckedBlockNumberAsync(NetworkName);

                lock (_blockLock)
                {
                    _invoiceLastProcessedBlock = lastDbBlock;
                }

                if (_invoiceLastProcessedBlock > 0)
                {
                    return _invoiceLastProcessedBlock.ToHexBigInteger();
                }

                var latestBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

                lock (_blockLock)
                {
                    _invoiceLastProcessedBlock = latestBlock;

                    return latestBlock;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} GetInvoiceLastProcessedBlock failed", InvoiceLogPrefix);

                SwitchRpc();
                InitializeClients();
                throw;
            }
        }

        private async Task EnsureInvoiceEventCursorIncludesLockedEventsAsync()
        {
            var combinedBlock = await _transactionLogService.GetCombinedInvoiceEventLastCheckedBlockNumberAsync(NetworkName);

            lock (_blockLock)
            {
                if (_invoiceLastProcessedBlock < combinedBlock)
                    _invoiceLastProcessedBlock = combinedBlock;
            }
        }

        #endregion




        #region Stake

        private async Task SubscribeToStakeContractEventsAsync(CancellationToken cancellationToken)
        {
            var subscription = new EthLogsObservableSubscription(_webSocketClient);

            var safeObservable = subscription
                    .GetSubscriptionDataResponsesAsObservable()
                    .Where(log => log.Address.IsTheSameAddress(_stakeContractAddress))
                    .Select(log => Observable.FromAsync(() => ProcessStakeContractEventLogAsync(log, cancellationToken)))
                    .Concat();

            _stakeContractEventsSubscription = safeObservable.Subscribe(
                _ => { },
                ex =>
                {
                    _logger.LogError(
                        ex,
                        "{Prefix} Stake subscription error",
                        StakeLogPrefix
                    );
                },
                () =>
                {
                    _logger.LogWarning(
                        "{Prefix} Stake subscription completed",
                        StakeLogPrefix
                    );
                });

            var filter = new NewFilterInput
            {
                Address = new[] { _stakeContractAddress },
                FromBlock = new BlockParameter(await GetStakeLastProcessedBlock(cancellationToken))
            };

            await subscription.SubscribeAsync(filter);

            _logger.LogInformation("{Prefix} Stake subscription active: {Address}", StakeLogPrefix, _stakeContractAddress);
        }

        private async Task ProcessStakeContractEventLogAsync(FilterLog log, CancellationToken cancellationToken)
        {
            try
            {
                var depositCreated = log.DecodeEvent<DepositCreatedEventDTO>();

                if (depositCreated != null)
                {
                    await CreateDepositCreatedLogAsync(log, depositCreated, cancellationToken);

                    return;
                }

                //var earlyWithdrawn = log.DecodeEvent<EarlyWithdrawnEventDTO>();

                //if (earlyWithdrawn != null)
                //{
                //    await CreateEarlyWithdrawnLogAsync(log, earlyWithdrawn, cancellationToken);

                //    return;
                //}

                var profitWithdrawn = log.DecodeEvent<ProfitWithdrawnEventDTO>();

                if (profitWithdrawn != null)
                {
                    await CreateProfitWithdrawnLogAsync(log, profitWithdrawn, cancellationToken);

                    return;
                }

                var withdrawn = log.DecodeEvent<WithdrawnEventDTO>();

                if (withdrawn != null)
                {
                    await CreateWithdrawnLogAsync(log, withdrawn, cancellationToken);

                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "{Prefix} Stake event decode failed",
                    StakeLogPrefix
                );
            }
        }

        private async Task CreateDepositCreatedLogAsync(FilterLog log, EventLog<DepositCreatedEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var depositId = ByteArray32ToHex(eLog.Event.DepositId);

                _logger.LogInformation(
                  "{prefix} DepositCreated | DepositId: {DepositId}, Depositor: {Depositor}, Token: {Token}",
                  StakeLogPrefix,
                  depositId,
                  eLog.Event.Depositor,
                  eLog.Event.Token
              );

                SentrySdk.CaptureMessage(
                   $"{StakeLogPrefix} DepositCreated | DepositId: {depositId}, Depositor: {eLog.Event.Depositor}"
               );

                await _transactionLogService.CreateDepositCreatedLogAsync(
                    new _TransactionLog.DTOs.DepositCreatedLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,
                        DepositId = depositId,
                        Depositor = eLog.Event.Depositor,
                        Token = eLog.Event.Token,
                        LockDuration = eLog.Event.LockDuration,
                        Principal = eLog.Event.Principal,
                        Profit = eLog.Event.Profit,
                        UnlocksAt = eLog.Event.UnlocksAt,
                        EventType = Domain.Collections.BlockchainEventType.DepositCreated,
                        Network = NetworkName
                    });

                _lastEventReceived = DateTime.UtcNow;

                lock (_blockLock)
                {
                    _stakeLastProcessedBlock = BigInteger.Max(_stakeLastProcessedBlock, log.BlockNumber.Value + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} DepositCreated failed", StakeLogPrefix
                );

                throw;
            }
        }

        //private async Task CreateEarlyWithdrawnLogAsync(FilterLog log, EventLog<EarlyWithdrawnEventDTO> eLog, CancellationToken cancellationToken)
        //{
        //    try
        //    {
        //        var depositId = ByteArray32ToHex(eLog.Event.DepositId);
        //        _logger.LogInformation(
        //          "{prefix} EarlyWithdrawn | DepositId: {DepositId}, Depositor: {Depositor}",
        //          StakeLogPrefix,
        //          depositId,
        //          eLog.Event.Depositor
        //      );

        //        SentrySdk.CaptureMessage(
        //            $"{StakeLogPrefix} EarlyWithdrawn | DepositId: {depositId}, Depositor: {eLog.Event.Depositor}"
        //        );
        //        await _transactionLogService.CreateEarlyWithdrawnLogAsync(
        //            new _TransactionLog.DTOs.EarlyWithdrawnLog
        //            {
        //                Hash = log.TransactionHash,
        //                Address = log.Address,
        //                BlockNumber = log.BlockNumber!.Value,
        //                DepositId = depositId,
        //                Depositor = eLog.Event.Depositor,
        //                WithdrawAmount = eLog.Event.WithdrawAmount,
        //                ProfitAmount = eLog.Event.ProfitAmount,
        //                FinalPayoutAmount = eLog.Event.FinalPayoutAmount,
        //                ClaimedProfitAmount = eLog.Event.ClaimedProfitAmount,
        //                EventType = Domain.Collections.BlockchainEventType.EarlyWithdrawn,
        //                Network = NetworkName
        //            });

        //        _lastEventReceived = DateTime.UtcNow;

        //        //lock (_blockLock)
        //        //{
        //        //    _lastProcessedBlock = BigInteger.Max(
        //        //        _lastProcessedBlock,
        //        //        log.BlockNumber.Value + 1
        //        //    );
        //        //}
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "{Prefix} EarlyWithdrawn failed", StakeLogPrefix);

        //        throw;
        //    }
        //}

        private async Task CreateProfitWithdrawnLogAsync(FilterLog log, EventLog<ProfitWithdrawnEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var depositId = ByteArray32ToHex(eLog.Event.DepositId);
                _logger.LogInformation(
                  "{prefix} ProfitWithdrawn | DepositId: {DepositId}, Depositor: {Depositor}, Profit: {Profit}",
                  StakeLogPrefix,
                  depositId,
                  eLog.Event.Depositor,
                  eLog.Event.Profit
              );

                SentrySdk.CaptureMessage(
                    $"{StakeLogPrefix} ProfitWithdrawn | DepositId: {depositId}, Depositor: {eLog.Event.Depositor}, Profit: {eLog.Event.Profit}"
                );

                await _transactionLogService.CreateProfitWithdrawnLogAsync(
                    new _TransactionLog.DTOs.ProfitWithdrawnLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,
                        DepositId = depositId,
                        Depositor = eLog.Event.Depositor,
                        Token = eLog.Event.Token,
                        Profit = eLog.Event.Profit,
                        EventType = Domain.Collections.BlockchainEventType.ProfitWithdrawn,
                        Network = NetworkName
                    });

                _lastEventReceived = DateTime.UtcNow;

                //lock (_blockLock)
                //{
                //    _lastProcessedBlock = BigInteger.Max(
                //        _lastProcessedBlock,
                //        log.BlockNumber.Value + 1
                //    );
                //}
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "{Prefix} ProfitWithdrawn failed",
                    StakeLogPrefix
                );

                throw;
            }
        }

        private async Task CreateWithdrawnLogAsync(FilterLog log, EventLog<WithdrawnEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var depositId = ByteArray32ToHex(eLog.Event.DepositId);


                _logger.LogInformation(
                   "{prefix} Withdrawn | DepositId: {DepositId}, Depositor: {Depositor}",
                   StakeLogPrefix,
                   depositId,
                   eLog.Event.Depositor
               );

                SentrySdk.CaptureMessage(
                    $"{StakeLogPrefix} Withdrawn | DepositId: {depositId}, Depositor: {eLog.Event.Depositor}"
                );


                await _transactionLogService.CreateWithdrawnLogAsync(
                    new _TransactionLog.DTOs.WithdrawnLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,
                        DepositId = depositId,
                        Depositor = eLog.Event.Depositor,
                        Principal = eLog.Event.Principal,
                        Profit = eLog.Event.Profit,
                        TotalPayout = eLog.Event.TotalPayout,
                        EventType = Domain.Collections.BlockchainEventType.WithdrawnAll,
                        Network = NetworkName
                    });

                _lastEventReceived = DateTime.UtcNow;

                //lock (_blockLock)
                //{
                //    _lastProcessedBlock = BigInteger.Max(
                //        _lastProcessedBlock,
                //        log.BlockNumber.Value + 1
                //    );
                //}
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "{Prefix} Withdrawn failed",
                    StakeLogPrefix
                );

                throw;
            }
        }

        private async Task<HexBigInteger> GetStakeLastProcessedBlock(CancellationToken cancellationToken)
        {
            try
            {
                lock (_blockLock)
                {
                    if (_stakeLastProcessedBlock > 0)
                    {
                        return _stakeLastProcessedBlock.ToHexBigInteger();
                    }
                }

                var lastDbBlock = await _transactionLogService.GetDepositLastCheckedBlockNumberAsync(NetworkName);

                lock (_blockLock)
                {
                    _stakeLastProcessedBlock = lastDbBlock;
                }

                if (_stakeLastProcessedBlock > 0)
                {
                    return _stakeLastProcessedBlock.ToHexBigInteger();
                }

                var latestBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

                lock (_blockLock)
                {
                    _stakeLastProcessedBlock = latestBlock;

                    return latestBlock;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} GetStakeLastProcessedBlock failed", StakeLogPrefix);

                SwitchRpc();
                InitializeClients();

                throw;
            }
        }

        #endregion



        #region Helpers

        private static DateTime? ConvertUnixSecondsToDateTime(BigInteger? unixSeconds)
        {
            if (!unixSeconds.HasValue)
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeSeconds((long)unixSeconds.Value).UtcDateTime;
        }

        private static string ByteArray32ToHex(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length != 32)
            {
                throw new ArgumentException("Input must be exactly 32 bytes for bytes32");
            }

            return bytes.ToHex();
        }

        #endregion



        #region Dispose

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed)
            {
                return;
            }

            //_logger.LogInformation(
            //    "{Prefix} Service stopping...",
            //    CommonPrefix);

            try
            {
                await CleanupConnection();
            }
            finally
            {
                _isDisposed = true;

                await base.StopAsync(cancellationToken);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _contractEventsSubscription?.Dispose();

            _stakeContractEventsSubscription?.Dispose();

            _webSocketClient?.Dispose();

            _reconnectLock?.Dispose();

            _cleanupLock?.Dispose();

            _isDisposed = true;
        }

        #endregion
    }
}
