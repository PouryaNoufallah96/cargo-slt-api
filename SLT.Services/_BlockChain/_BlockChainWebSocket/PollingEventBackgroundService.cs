using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using SLT.Services._BlockChain._BlockChainWebSocket.DTOs;
using SLT.Services._BlockChain.DTOs.Settings;
using SLT.Services._TransactionLog;
using System.Numerics;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._BlockChain._BlockChainWebSocket
{
    public class PollingEventBackgroundService(
        ITransactionLogService transactionLogService,
        ILogger<PollingEventBackgroundService> _logger,
        BlockChainSettings _settings
    ) : BackgroundService, IHostedDependency
    {
        private BigInteger _lastProcessedBlock = 0;
        private readonly object _blockLock = new object();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("------------------ Polling missing logs before subscription restart...");

            _lastProcessedBlock = await GetLastProcessedBlock(stoppingToken);
            _logger.LogInformation($"starting block is : {_lastProcessedBlock}");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollMissingLogsAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
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
                    var _web3Client = new Web3(_settings.RpcUrl2);
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
                            _web3Client = new Web3(_settings.RpcUrl);
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

        private async Task PollMissingLogsAsync(CancellationToken cancellationToken)
        {
            var _web3Client = new Web3(_settings.RpcUrl2);
            BigInteger latestBlock = await _web3Client.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            if (_lastProcessedBlock >= latestBlock) return;

            const int blockChunk = 2000;
            BigInteger fromBlock = _lastProcessedBlock;

            while (fromBlock <= latestBlock)
            {
                BigInteger toBlock = BigInteger.Min(fromBlock + blockChunk - 1, latestBlock);

                var filter = new NewFilterInput
                {
                    FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
                    ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
                    Address = new[] { _settings.ContractAddress }
                };

                try
                {
                    var logs = await _web3Client.Eth.Filters.GetLogs.SendRequestAsync(filter);

                    foreach (var log in logs)
                    {
                        var filterLog = log as FilterLog;
                        if (filterLog == null) continue;

                        try
                        {
                            var created = log.DecodeEvent<InvoiceCreatedEventDTO>();
                            if (created != null)
                            {
                                await CreateInvoiceCreatedLog(log, created, cancellationToken);
                                continue;
                            }

                            var paid = log.DecodeEvent<InvoicePaidEventDTO>();
                            if (paid != null)
                            {
                                await CreateInvoicePaidLogAsync(log, paid, cancellationToken);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error decoding polled log");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling logs from {FromBlock} to {ToBlock}", fromBlock, toBlock);
                }

                fromBlock = toBlock + 1;
                await Task.Delay(3000);
            }

            lock (_blockLock)
            {
                _lastProcessedBlock = BigInteger.Max(_lastProcessedBlock, latestBlock);
                _logger.LogInformation("-------------- Poling until {latestBlock}", latestBlock);
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

            return  bytes.ToHex();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Shutting down blockchain Polling service...");
            await base.StopAsync(cancellationToken);
        }
    }
}
