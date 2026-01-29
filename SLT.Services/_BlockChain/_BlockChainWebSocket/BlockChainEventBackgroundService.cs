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
        private BigInteger _lastProcessedBlock = 0;
        private int _reconnectAttempts = 0;
        private DateTime _lastEventReceived = DateTime.UtcNow;
        private readonly SemaphoreSlim _reconnectLock = new(1, 1);
        private bool _isCleaningUp = false;
        private readonly SemaphoreSlim _cleanupLock = new(1, 1);
        private IDisposable _contractEventsSubscription;

        private bool _useSecondaryWsUrl = false;
        private Web3 _web3;
        private StreamingWebSocketClient _webSocketClient;
        private readonly object _blockLock = new();
        private bool _isDisposed = false;
        private readonly AvailableTokensSettings _availableTokensSettings;


        public BlockChainEventBackgroundService(
            IServiceScopeFactory scopeFactory,
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
            _logger.LogInformation("Blockchain Event Service starting...");
            _lastProcessedBlock = await GetLastProcessedBlock(stoppingToken);
            _logger.LogInformation($"starting block is : {_lastProcessedBlock}");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TryConnectWithRetryAsync(stoppingToken);
                    _lastEventReceived = DateTime.UtcNow;

                    _logger.LogInformation("-----------------------Successfully connected and subscribed to blockchain events");

                    while (_webSocketClient?.IsStarted == true && !stoppingToken.IsCancellationRequested)
                    {
                        var now = DateTime.UtcNow;
                        if ((now - _lastEventReceived).TotalMinutes > 2)
                        {
                            _logger.LogWarning("----------- No blockchain events received in the last 3 minutes {time}. Reconnecting...", now);
                            await Task.Delay(2000, stoppingToken);
                            await TryConnectWithRetryAsync(stoppingToken);

                            _lastEventReceived = DateTime.UtcNow;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }

                    if (_webSocketClient != null && !_webSocketClient.IsStarted)
                    {
                        _logger.LogWarning("WebSocket stopped unexpectedly, reconnecting...");
                        await Task.Delay(2000, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Service shutdown requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in blockchain event service");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("Blockchain Event Service stopped.");
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
                        _logger.LogWarning(ex, "Connection attempt failed. Will retry...");
                        await Task.Delay(CalculateReconnectDelay(), stoppingToken);
                    }
                }

                if (_reconnectAttempts >= _settings.MaxReconnectAttempts)
                {
                    _logger.LogCritical("Max reconnection attempts reached. Waiting before next try...");
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
            _logger.LogInformation("...........ConnectAndSubscribe touched............");


            await CleanupConnection();

            var currestWsUrl = GetCurrentWsUrl();
            _webSocketClient = new StreamingWebSocketClient(currestWsUrl);
            _web3 = new Web3(currestWsUrl);


            try
            {
                await _webSocketClient.StartAsync();

                await SubscribeToContractEventsAsync(cancellationToken);
                _logger.LogInformation("ContractEvents subscription is active.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting/subscribing. Will reconnect...");
                throw;
            }
        }

        private async Task CleanupConnection()
        {
            if (!await _cleanupLock.WaitAsync(0))
            {
                _logger.LogInformation("Cleanup already in progress, skipping...");
                return;
            }

            try
            {
                _logger.LogInformation("Starting cleanup...");

                _contractEventsSubscription?.Dispose();
                _contractEventsSubscription = null;

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
                        _logger.LogWarning(ex, "WebSocket StopAsync failed or already stopped");
                    }

                    try
                    {
                        _webSocketClient.Dispose();
                    }
                    catch (SemaphoreFullException ex)
                    {
                        _logger.LogWarning(ex, "Ignoring SemaphoreFullException from WebSocket.Dispose()");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "WebSocket Dispose failed");
                    }

                    _webSocketClient = null;
                }

                _logger.LogInformation("Cleanup completed successfully.");
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
            _logger.LogInformation("WebSocket URL : {Url}", wss);
            return wss;
        }


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
                FromBlock = new BlockParameter(await GetLastProcessedBlock(cancellationToken))
            };

            await subscription.SubscribeAsync(filter);
        }

        private async Task<HexBigInteger> GetLastProcessedBlock(CancellationToken cancellationToken)
        {

            try
            {


                lock (_blockLock)
                {
                    if (_lastProcessedBlock > 0)
                        return _lastProcessedBlock.ToHexBigInteger();
                }

                var lastDbBlock = await transactionLogService.GetLastCheckedBlockNumberAsync();

                lock (_blockLock)
                {
                    _lastProcessedBlock = lastDbBlock;
                }

                if (_lastProcessedBlock > 0)
                    return _lastProcessedBlock.ToHexBigInteger();

                try
                {
                    var _web3Client = new Web3(blockChainSettings.RpcUrl2);
                    try
                    {

                        var latestBlockNumber = await _web3Client.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                        lock (_blockLock)
                        {
                            _lastProcessedBlock = latestBlockNumber;
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
                                _lastProcessedBlock = latestBlockNumber;
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

        private async Task CreateInvoiceCreatedLog(
        FilterLog log,
        EventLog<InvoiceCreatedEventDTO> eLog,
        CancellationToken cancellationToken)
        {
            try
            {
                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);

                _logger.LogInformation(
                    "InvoiceCreated | InvoiceId: {InvoiceId}, Creator: {Creator}, Token: {Token}, USD: {UsdAmount}",
                    invoiceId,
                    eLog.Event.Creator,
                    eLog.Event.Token,
                    eLog.Event.UsdAmount
                );

                SentrySdk.CaptureMessage(
                    $"InvoiceCreated | InvoiceId: {invoiceId}, Creator: {eLog.Event.Creator}, Token: {eLog.Event.Token}, USD: {eLog.Event.UsdAmount}"
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
                        EventType = Domain.Collections.BlockchainEventType.InvoiceCreated
                    }
                );

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


        private async Task CreateInvoicePaidLogAsync(
        FilterLog log,
        EventLog<InvoicePaidEventDTO> eLog,
        CancellationToken cancellationToken)
        {
            try
            {
                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);

                _logger.LogInformation(
                    "InvoicePaid | InvoiceId: {InvoiceId}, Payer: {Payer}, Token: {Token}, PayAmount: {PayAmount}",
                    invoiceId,
                    eLog.Event.Payer,
                    eLog.Event.Token,
                    eLog.Event.PayAmount
                );

                SentrySdk.CaptureMessage(
                    $"InvoicePaid | InvoiceId: {invoiceId}, Payer: {eLog.Event.Payer}, Token: {eLog.Event.Token}, PayAmount: {eLog.Event.PayAmount}"
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
                        EventType = Domain.Collections.BlockchainEventType.InvoicePaid
                    }
                );

                lock (_blockLock)
                {
                    _lastProcessedBlock = BigInteger.Max(
                        _lastProcessedBlock,
                        log.BlockNumber.Value + 1
                    );
                }
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


        private static string ByteArray32ToHex(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (bytes.Length != 32)
                throw new ArgumentException("Input must be exactly 32 bytes for bytes32");

            return "0x" + bytes.ToHex();
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
                _webSocketClient?.Dispose();
                _reconnectLock?.Dispose();
                _cleanupLock?.Dispose();
                _isDisposed = true;
            }
        }
    }
}
