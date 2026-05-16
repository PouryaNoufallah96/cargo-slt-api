using SLT.Services._TransactionLog;
using Microsoft.Extensions.DependencyInjection;
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
using SLT.Services._Price.DTOs.Settings;
using System.Numerics;
using System.Reactive.Linq;
using Utilities.Extension;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._BlockChain._BlockChainWebSocket
{
    public class BlockChainEventBackgroundService : BackgroundService, IHostedDependency
    {
        private readonly BlockChainSettings blockChainSettings;
        private readonly ITransactionLogService transactionLogService;
        private readonly ILogger<BlockChainEventBackgroundService> _logger;
        private readonly BlockchainWebSocketSetting _settings;
        private BigInteger _invoiceLastProcessedBlock = 0;
        private BigInteger _stakeLastProcessedBlock = 0;
        private int _reconnectAttempts = 0;
        private DateTime _lastEventReceived = DateTime.UtcNow;
        private readonly SemaphoreSlim _reconnectLock = new(1, 1);
        private bool _isCleaningUp = false;
        private readonly SemaphoreSlim _cleanupLock = new(1, 1);
        private IDisposable _contractEventsSubscription;
        private IDisposable _stakeContractEventsSubscription;

        private bool _useSecondaryWsUrl = false;
        private Web3 _web3;
        private StreamingWebSocketClient _webSocketClient;
        private readonly object _blockLock = new();
        private bool _isDisposed = false;
        private readonly AvailableTokensSettings _availableTokensSettings;


        public BlockChainEventBackgroundService(
            BlockChainSettings blockChainSettings,
            ITransactionLogService _transactionLogService,
            ILogger<BlockChainEventBackgroundService> logger,
            BlockchainWebSocketSetting settings)
        {
            this.blockChainSettings = blockChainSettings;
            transactionLogService = _transactionLogService;
            _logger = logger;
            _settings = settings;
            _web3 = new Web3(settings.WsUrl2);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _invoiceLastProcessedBlock = await GetInvoiceLastProcessedBlock(stoppingToken);
            _logger.LogInformation($"BEP20 invoices starting block is : {_invoiceLastProcessedBlock}");

            _stakeLastProcessedBlock = await GetStakeLastProcessedBlock(stoppingToken);
            _logger.LogInformation($"BEP20 stake side starting block is : {_stakeLastProcessedBlock}");


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
                    await Task.Delay(5000, stoppingToken);
                }
            }

        }

        private async Task TryConnectWithRetryAsync(CancellationToken stoppingToken)
        {
            if (!await _reconnectLock.WaitAsync(0, stoppingToken))
            {
                return;
            }

            try
            {
                _reconnectAttempts = 0;

                while (!stoppingToken.IsCancellationRequested &&
                       _reconnectAttempts < _settings.MaxReconnectAttempts)
                {
                    try
                    {
                        _logger.LogInformation($"Attempting to connect (Attempt {_reconnectAttempts + 1}/{_settings.MaxReconnectAttempts})");
                        await ConnectAndSubscribe(stoppingToken);

                        _reconnectAttempts = 0;
                        return;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _reconnectAttempts++;
                        //_logger.LogWarning(ex, "Connection attempt failed. Will retry...");
                        await Task.Delay(CalculateReconnectDelay(), stoppingToken);
                    }
                }

                if (_reconnectAttempts >= _settings.MaxReconnectAttempts)
                {
                    //_logger.LogCritical("Max reconnection attempts reached. Waiting before next try...");
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
            double delaySeconds = Math.Min(
                Math.Pow(2, _reconnectAttempts) * _settings.ReconnectInterval,
                300);
            return TimeSpan.FromSeconds(delaySeconds);
        }


        private async Task ConnectAndSubscribe(CancellationToken cancellationToken)
        {


            await CleanupConnection();

            var currestWsUrl = GetCurrentWsUrl();
            _webSocketClient = new StreamingWebSocketClient(currestWsUrl);
            _web3 = new Web3(currestWsUrl);


            try
            {
                await _webSocketClient.StartAsync();

                await SubscribeToContractEventsAsync(cancellationToken);
                await SubscribeToStakeContractEventsAsync(cancellationToken);


                _logger.LogInformation("BEP20 subscriptions are active.");

            }
            catch (Exception ex)
            {
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
                _logger.LogInformation("Starting cleanup...");

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
                        //_logger.LogWarning(ex, "WebSocket StopAsync failed or already stopped");
                    }

                    try
                    {
                        _webSocketClient.Dispose();
                    }
                    catch (SemaphoreFullException ex)
                    {
                        //_logger.LogWarning(ex, "Ignoring SemaphoreFullException from WebSocket.Dispose()");
                    }
                    catch (Exception ex)
                    {
                        //_logger.LogWarning(ex, "WebSocket Dispose failed");
                    }

                    _webSocketClient = null;
                }

                //_logger.LogInformation("Cleanup completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup.");
            }
            finally
            {
                _cleanupLock.Release();
            }
        }

        private string GetCurrentWsUrl()
        {
            var wss = _useSecondaryWsUrl ? _settings.WsUrl : _settings.WsUrl2;
            _useSecondaryWsUrl = !_useSecondaryWsUrl;
            //_logger.LogInformation("WebSocket URL : {Url}", wss);
            return wss;
        }



        #region Invoice

        private async Task SubscribeToContractEventsAsync(CancellationToken cancellationToken)
        {
            var subscription = new EthLogsObservableSubscription(_webSocketClient);

            var safeObservable = subscription.GetSubscriptionDataResponsesAsObservable()
           .Where(log => log.Address.IsTheSameAddress(_settings.ContractAddress))
           .Select(log => Observable.FromAsync(() => ProcessContractEventLogAsync(log, cancellationToken)))
           .Concat();

            _contractEventsSubscription = safeObservable.Subscribe(
                _ => { },
                async ex =>
                {
                    _logger.LogError(ex, "Error in subscription. Reconnecting...");
                },
                () =>
                {
                    _logger.LogWarning("Subscription completed unexpectedly. Reconnecting...");
                });

            var filter = new NewFilterInput
            {
                Address = new[] { _settings.ContractAddress },
                FromBlock = new BlockParameter(await GetInvoiceLastProcessedBlock(cancellationToken))
            };

            await subscription.SubscribeAsync(filter);
        }

        private async Task<HexBigInteger> GetInvoiceLastProcessedBlock(CancellationToken cancellationToken)
        {

            try
            {


                lock (_blockLock)
                {
                    if (_invoiceLastProcessedBlock > 0)
                        return _invoiceLastProcessedBlock.ToHexBigInteger();
                }

                var lastDbBlock = await transactionLogService.GetInvoiceLastCheckedBlockNumberAsync("BEP20");

                lock (_blockLock)
                {
                    _invoiceLastProcessedBlock = lastDbBlock;
                }

                if (_invoiceLastProcessedBlock > 0)
                    return _invoiceLastProcessedBlock.ToHexBigInteger();

                try
                {
                    var _web3Client = new Web3(blockChainSettings.RpcUrl2);
                    try
                    {

                        var latestBlockNumber = await _web3Client.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                        lock (_blockLock)
                        {
                            _invoiceLastProcessedBlock = latestBlockNumber;
                            return latestBlockNumber;
                        }

                    }
                    catch (Exception)
                    {
                        try
                        {
                            _web3Client = new Web3(blockChainSettings.RpcUrl);
                            var latestBlockNumber = await _web3Client.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                            lock (_blockLock)
                            {
                                _invoiceLastProcessedBlock = latestBlockNumber;
                                return latestBlockNumber;
                            }
                        }
                        catch (Exception)
                        {

                            throw;
                        }


                    }


                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                    throw;
                }

            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                throw;
            }
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

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decoding blockchain event");
            }
        }

        private async Task CreateInvoiceCreatedLog(FilterLog log, EventLog<InvoiceCreatedEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);

                _logger.LogInformation(
                    "BEP20 InvoiceCreated | InvoiceId: {InvoiceId}, Creator: {Creator}, Token: {Token}, USD: {UsdAmount}",
                    invoiceId,
                    eLog.Event.Creator,
                    eLog.Event.Token,
                    eLog.Event.UsdAmount
                );

                SentrySdk.CaptureMessage(
                    $"BEP20 InvoiceCreated | InvoiceId: {invoiceId}, Creator: {eLog.Event.Creator}, Token: {eLog.Event.Token}, USD: {eLog.Event.UsdAmount}"
                );

                var unlockDate = ConvertUnixSecondsToDateTime(eLog.Event.UnlockTime);

                await transactionLogService.CreateInvoiceCreatedAsync(
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
                        Network = "BEP20"
                    }
                );

                _lastEventReceived = DateTime.UtcNow;

                lock (_blockLock)
                {
                    _invoiceLastProcessedBlock = BigInteger.Max(
                        _invoiceLastProcessedBlock,
                        log.BlockNumber.Value + 1
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing InvoiceCreated event. TxHash: {TxHash}",
                    log.TransactionHash
                );
                throw;
            }
        }

        private async Task CreateInvoicePaidLogAsync(FilterLog log, EventLog<InvoicePaidEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);

                _logger.LogInformation(
                    "BEP20 InvoicePaid | InvoiceId: {InvoiceId}, Payer: {Payer}, Token: {Token}, PayAmount: {PayAmount}",
                    invoiceId,
                    eLog.Event.Payer,
                    eLog.Event.Token,
                    eLog.Event.PayAmount
                );

                SentrySdk.CaptureMessage(
                    $"BEP20 InvoicePaid | InvoiceId: {invoiceId}, Payer: {eLog.Event.Payer}, Token: {eLog.Event.Token}, PayAmount: {eLog.Event.PayAmount}"
                );

                var payAmountDecimal = Web3.Convert.FromWei(eLog.Event.PayAmount);

                await transactionLogService.CreateInvoicePaidAsync(
                    new _TransactionLog.DTOs.InvoicePaidLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,
                        InvoiceId = invoiceId,
                        Payer = eLog.Event.Payer,
                        Token = eLog.Event.Token,
                        PayAmount = payAmountDecimal,
                        EventType = Domain.Collections.BlockchainEventType.InvoicePaid,
                        Network = "BEP20"
                    }
                );

                _lastEventReceived = DateTime.UtcNow;



            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing InvoicePaid event. TxHash: {TxHash}",
                    log.TransactionHash
                );
                throw;
            }
        }

        private static DateTime? ConvertUnixSecondsToDateTime(BigInteger? unixSeconds)
        {
            if (!unixSeconds.HasValue)
                return null;

            if (unixSeconds.Value > long.MaxValue || unixSeconds.Value < long.MinValue)
                throw new ArgumentOutOfRangeException(nameof(unixSeconds), "Unix timestamp is out of range.");

            return DateTimeOffset
                .FromUnixTimeSeconds((long)unixSeconds.Value)
                .UtcDateTime;
        }
        #endregion



        #region Stake

        private async Task SubscribeToStakeContractEventsAsync(CancellationToken cancellationToken)
        {
            var subscription = new EthLogsObservableSubscription(_webSocketClient);

            var safeObservable = subscription.GetSubscriptionDataResponsesAsObservable()
                .Where(log => log.Address.IsTheSameAddress(blockChainSettings.BEP20StakeContractAddress))
                .Select(log => Observable.FromAsync(() => ProcessStakeContractEventLogAsync(log, cancellationToken)))
                .Concat();

            _stakeContractEventsSubscription = safeObservable.Subscribe(
                _ => { },
                ex =>
                {
                    _logger.LogError(ex, "Error in STAKE subscription.");
                },
                () =>
                {
                    _logger.LogWarning("STAKE subscription completed unexpectedly.");
                });

            var filter = new NewFilterInput
            {
                Address = new[] { blockChainSettings.BEP20StakeContractAddress },
                FromBlock = new BlockParameter(await GetStakeLastProcessedBlock(cancellationToken))
            };

            await subscription.SubscribeAsync(filter);

            _logger.LogInformation(
                "BEP20 Stake Contract subscription is active. Address: {Address}",
                blockChainSettings.BEP20StakeContractAddress
            );
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

                var earlyWithdrawn = log.DecodeEvent<EarlyWithdrawnEventDTO>();
                if (earlyWithdrawn != null)
                {
                    await CreateEarlyWithdrawnLogAsync(log, earlyWithdrawn, cancellationToken);
                    return;
                }

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
                _logger.LogError(ex, "Error decoding STAKE blockchain event");
            }
        }

        private async Task CreateDepositCreatedLogAsync(FilterLog log, EventLog<DepositCreatedEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var depositId = ByteArray32ToHex(eLog.Event.DepositId);

                _logger.LogInformation(
                    "BEP20 DepositCreated | DepositId: {DepositId}, Depositor: {Depositor}, Token: {Token}",
                    depositId,
                    eLog.Event.Depositor,
                    eLog.Event.Token
                );

                await transactionLogService.CreateDepositCreatedLogAsync(
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
                        Network = "BEP20"
                    }
                );

                _lastEventReceived = DateTime.UtcNow;

                lock (_blockLock)
                {
                    _invoiceLastProcessedBlock = BigInteger.Max(
                        _invoiceLastProcessedBlock,
                        log.BlockNumber.Value + 1
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing DepositCreated event. TxHash: {TxHash}",
                    log.TransactionHash
                );

                throw;
            }
        }

        private async Task CreateEarlyWithdrawnLogAsync(FilterLog log, EventLog<EarlyWithdrawnEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var depositId = ByteArray32ToHex(eLog.Event.DepositId);

                _logger.LogInformation(
                    "BEP20 EarlyWithdrawn | DepositId: {DepositId}, Depositor: {Depositor}",
                    depositId,
                    eLog.Event.Depositor
                );

                SentrySdk.CaptureMessage(
                    $"BEP20 EarlyWithdrawn | DepositId: {depositId}, Depositor: {eLog.Event.Depositor}"
                );

                await transactionLogService.CreateEarlyWithdrawnLogAsync(
                    new _TransactionLog.DTOs.EarlyWithdrawnLog
                    {
                        Hash = log.TransactionHash,
                        Address = log.Address,
                        BlockNumber = log.BlockNumber!.Value,

                        DepositId = depositId,
                        Depositor = eLog.Event.Depositor,

                        WithdrawAmount = eLog.Event.WithdrawAmount,
                        ProfitAmount = eLog.Event.ProfitAmount,
                        FinalPayoutAmount = eLog.Event.FinalPayoutAmount,
                        ClaimedProfitAmount = eLog.Event.ClaimedProfitAmount,

                        EventType = Domain.Collections.BlockchainEventType.EarlyWithdrawn,
                        Network = "BEP20"
                    }
                );

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
                    "Error processing EarlyWithdrawn event. TxHash: {TxHash}",
                    log.TransactionHash
                );

                throw;
            }
        }

        private async Task CreateProfitWithdrawnLogAsync(FilterLog log, EventLog<ProfitWithdrawnEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var depositId = ByteArray32ToHex(eLog.Event.DepositId);

                _logger.LogInformation(
                    "BEP20 ProfitWithdrawn | DepositId: {DepositId}, Depositor: {Depositor}, Profit: {Profit}",
                    depositId,
                    eLog.Event.Depositor,
                    eLog.Event.Profit
                );

                SentrySdk.CaptureMessage(
                    $"BEP20 ProfitWithdrawn | DepositId: {depositId}, Depositor: {eLog.Event.Depositor}, Profit: {eLog.Event.Profit}"
                );

                await transactionLogService.CreateProfitWithdrawnLogAsync(
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
                        Network = "BEP20"
                    }
                );

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
                    "Error processing ProfitWithdrawn event. TxHash: {TxHash}",
                    log.TransactionHash
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
                    "BEP20 Withdrawn | DepositId: {DepositId}, Depositor: {Depositor}",
                    depositId,
                    eLog.Event.Depositor
                );

                SentrySdk.CaptureMessage(
                    $"BEP20 Withdrawn | DepositId: {depositId}, Depositor: {eLog.Event.Depositor}"
                );

                await transactionLogService.CreateWithdrawnLogAsync(
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
                        Network = "BEP20"
                    }
                );

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
                    "Error processing Withdrawn event. TxHash: {TxHash}",
                    log.TransactionHash
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
                        return _stakeLastProcessedBlock.ToHexBigInteger();
                }

                var lastDbBlock =
                    await transactionLogService.GetDepositLastCheckedBlockNumberAsync("BEP20");

                lock (_blockLock)
                {
                    _stakeLastProcessedBlock = lastDbBlock;
                }

                if (_stakeLastProcessedBlock > 0)
                    return _stakeLastProcessedBlock.ToHexBigInteger();

                try
                {
                    var web3Client = new Web3(blockChainSettings.RpcUrl);

                    try
                    {
                        var latestBlockNumber =
                            await web3Client.Eth.Blocks.GetBlockNumber.SendRequestAsync();

                        lock (_blockLock)
                        {
                            _stakeLastProcessedBlock = latestBlockNumber;
                            return latestBlockNumber;
                        }
                    }
                    catch
                    {
                        web3Client = new Web3(blockChainSettings.RpcUrl);

                        var latestBlockNumber =
                            await web3Client.Eth.Blocks.GetBlockNumber.SendRequestAsync();

                        lock (_blockLock)
                        {
                            _stakeLastProcessedBlock = latestBlockNumber;
                            return latestBlockNumber;
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                    throw;
                }
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                throw;
            }
        }

        #endregion



        private static string ByteArray32ToHex(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (bytes.Length != 32)
                throw new ArgumentException("Input must be exactly 32 bytes for bytes32");

            return bytes.ToHex();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed) return;

            _logger.LogInformation("Shutting down blockchain event service...");

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
            if (!_isDisposed)
            {
                _contractEventsSubscription?.Dispose();
                _stakeContractEventsSubscription?.Dispose();

                _webSocketClient?.Dispose();

                _reconnectLock?.Dispose();
                _cleanupLock?.Dispose();

                _isDisposed = true;
            }
        }
    }
}
