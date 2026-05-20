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
using System.Reactive.Linq;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._BlockChain._BlockChainWebSocket
{
    public class PollingERC20EventBackgroundService : BackgroundService, IHostedDependency
    {
        #region Prefixes

        private const string InvoiceLogPrefix = "[ERC20-INVOICE-POLLING]";
        private const string StakeLogPrefix = "[ERC20-STAKE-POLLING]";
        private const string CommonPrefix = "[ERC20-POLLING]";
        private const string NetworkName = "ERC20";
        #endregion

        #region Services

        private readonly ITransactionLogService _transactionLogService;
        private readonly ILogger<PollingERC20EventBackgroundService> _logger;
        private readonly BlockChainSettings _settings;

        #endregion

        #region Blockchain

        private readonly Web3 _web3;

        private readonly string _invoiceContractAddress;
        private readonly string _stakeContractAddress;

        #endregion

        #region Blocks

        private BigInteger _lastinvoiceProcessedBlock = 0;
        private BigInteger _stakeLastProcessedBlock = 0;

        private readonly object _blockLock = new();

        #endregion

        public PollingERC20EventBackgroundService(
            ITransactionLogService transactionLogService,
            ILogger<PollingERC20EventBackgroundService> logger,
            BlockChainSettings settings)
        {
            _transactionLogService = transactionLogService;
            _logger = logger;
            _settings = settings;

            _web3 = new Web3(_settings.ERC20RpcUrl);
            _invoiceContractAddress = _settings.ERC20ContractAddress;
            _stakeContractAddress = _settings.ERC20StakeContractAddress;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var latestBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

                    var safeBlock = BigInteger.Max(latestBlock.Value - 10, BigInteger.Zero);

                    try
                    {
                        await PollInvoiceMissingLogsAsync(safeBlock, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "{Prefix} Invoice polling failed",
                            InvoiceLogPrefix);
                    }

                    try
                    {
                        await PollMissingStakeLogsAsync(safeBlock, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "{Prefix} Stake polling failed",
                            StakeLogPrefix);
                    }

                    await Task.Delay(
                        TimeSpan.FromSeconds(15),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "{Prefix} Fatal polling loop error",
                        CommonPrefix);

                    await Task.Delay(5000, stoppingToken);
                }
            }
        }


        #region Invoice
        private async Task PollInvoiceMissingLogsAsync(BigInteger latestBlock, CancellationToken cancellationToken)
        {
            if (_lastinvoiceProcessedBlock < 1)
            {
                _lastinvoiceProcessedBlock = await GetInvoiceLastProcessedBlock(cancellationToken);
            }

            if (_lastinvoiceProcessedBlock >= latestBlock) return;

            const int blockChunk = 2000;

            BigInteger fromBlock = _lastinvoiceProcessedBlock;

            while (fromBlock <= latestBlock)
            {
                BigInteger toBlock = BigInteger.Min(fromBlock + blockChunk - 1, latestBlock);

                var filter = new NewFilterInput
                {
                    FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
                    ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
                    Address = new[] { _invoiceContractAddress }
                };

                try
                {
                    var logs = await _web3.Eth.Filters.GetLogs.SendRequestAsync(filter);

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
                            _logger.LogError(ex, "{Prefix} Error decoding invoice log", InvoiceLogPrefix);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "{Prefix} Error polling invoice logs from {FromBlock} to {ToBlock}",
                        InvoiceLogPrefix,
                        fromBlock,
                        toBlock);
                }

                fromBlock = toBlock + 1;

                await Task.Delay(3000, cancellationToken);
            }

            lock (_blockLock)
            {
                _lastinvoiceProcessedBlock = BigInteger.Max(_lastinvoiceProcessedBlock, latestBlock);

                _logger.LogInformation(
                    "{Prefix} Polling completed until block {LatestBlock}",
                    InvoiceLogPrefix,
                    latestBlock);
            }
        }

        private async Task CreateInvoiceCreatedLog(FilterLog log, EventLog<InvoiceCreatedEventDTO> eLog, CancellationToken cancellationToken)
        {
            try
            {
                var invoiceId = ByteArray32ToHex(eLog.Event.InvoiceId);

                _logger.LogInformation(
                    "{prefix} InvoiceCreated | InvoiceId: {InvoiceId}, Creator: {Creator}, Token: {Token}, USD: {UsdAmount}",
                    InvoiceLogPrefix,
                    invoiceId,
                    eLog.Event.Creator,
                    eLog.Event.Token,
                    eLog.Event.UsdAmount
                );

                SentrySdk.CaptureMessage(
                    $"{InvoiceLogPrefix} InvoiceCreated | InvoiceId: {invoiceId}, Creator: {eLog.Event.Creator}, Token: {eLog.Event.Token}, USD: {eLog.Event.UsdAmount}"
                );

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
                    }
                );

                lock (_blockLock)
                {
                    _lastinvoiceProcessedBlock = BigInteger.Max(
                        _lastinvoiceProcessedBlock,
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
                    "{prefix} InvoicePaid | InvoiceId: {InvoiceId}, Payer: {Payer}, Token: {Token}, PayAmount: {PayAmount}",
                    InvoiceLogPrefix,
                    invoiceId,
                    eLog.Event.Payer,
                    eLog.Event.Token,
                    eLog.Event.PayAmount
                );

                SentrySdk.CaptureMessage(
                    $"{InvoiceLogPrefix} InvoicePaid | InvoiceId: {invoiceId}, Payer: {eLog.Event.Payer}, Token: {eLog.Event.Token}, PayAmount: {eLog.Event.PayAmount}"
                );

                var payAmountDecimal = Web3.Convert.FromWei(eLog.Event.PayAmount);

                await _transactionLogService.CreateInvoicePaidAsync(
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
                        Network = NetworkName
                    }
                );

                //lock (_blockLock)
                //{
                //    _lastinvoiceProcessedBlock = BigInteger.Max(
                //        _lastinvoiceProcessedBlock,
                //        log.BlockNumber.Value + 1
                //    );
                //}
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

        private async Task<HexBigInteger> GetInvoiceLastProcessedBlock(CancellationToken cancellationToken)
        {

            try
            {

                lock (_blockLock)
                {
                    if (_lastinvoiceProcessedBlock > 0)
                        return _lastinvoiceProcessedBlock.ToHexBigInteger();
                }

                var lastDbBlock = await _transactionLogService.GetInvoiceLastCheckedBlockNumberAsync(NetworkName);

                lock (_blockLock)
                {
                    _lastinvoiceProcessedBlock = lastDbBlock;
                }

                if (_lastinvoiceProcessedBlock > 0)
                    return _lastinvoiceProcessedBlock.ToHexBigInteger();

                try
                {
                    var _web3Client = new Web3(_settings.RpcUrl2);
                    try
                    {

                        var latestBlockNumber = await _web3Client.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                        lock (_blockLock)
                        {
                            _lastinvoiceProcessedBlock = latestBlockNumber;
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
                                _lastinvoiceProcessedBlock = latestBlockNumber;
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

        private async Task PollMissingStakeLogsAsync(BigInteger latestBlock, CancellationToken cancellationToken)
        {
            if (_stakeLastProcessedBlock < 1)
            {
                _stakeLastProcessedBlock = await GetStakeLastProcessedBlock(cancellationToken);
            }

            if (_stakeLastProcessedBlock >= latestBlock) return;

            const int blockChunk = 2000;

            BigInteger fromBlock = _stakeLastProcessedBlock;

            while (fromBlock <= latestBlock)
            {
                BigInteger toBlock = BigInteger.Min(fromBlock + blockChunk - 1, latestBlock);

                var filter = new NewFilterInput
                {
                    FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
                    ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
                    Address = new[] { _stakeContractAddress }
                };

                try
                {
                    var logs = await _web3.Eth.Filters.GetLogs.SendRequestAsync(filter);

                    foreach (var log in logs)
                    {
                        var filterLog = log as FilterLog;

                        if (filterLog == null)
                            continue;

                        try
                        {
                            var depositCreated = log.DecodeEvent<DepositCreatedEventDTO>();
                            if (depositCreated != null)
                            {
                                await CreateDepositCreatedLogAsync(log, depositCreated, cancellationToken);
                                continue;
                            }

                            var earlyWithdrawn = log.DecodeEvent<EarlyWithdrawnEventDTO>();
                            if (earlyWithdrawn != null)
                            {
                                await CreateEarlyWithdrawnLogAsync(log, earlyWithdrawn, cancellationToken);
                                continue;
                            }

                            var profitWithdrawn = log.DecodeEvent<ProfitWithdrawnEventDTO>();
                            if (profitWithdrawn != null)
                            {
                                await CreateProfitWithdrawnLogAsync(log, profitWithdrawn, cancellationToken);
                                continue;
                            }

                            var withdrawn = log.DecodeEvent<WithdrawnEventDTO>();
                            if (withdrawn != null)
                            {
                                await CreateWithdrawnLogAsync(log, withdrawn, cancellationToken);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "{Prefix} Error decoding stake log",
                                StakeLogPrefix);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "{Prefix} Error polling stake logs from {FromBlock} to {ToBlock}",
                        StakeLogPrefix,
                        fromBlock,
                        toBlock);
                }

                fromBlock = toBlock + 1;

                await Task.Delay(3000, cancellationToken);
            }

            lock (_blockLock)
            {
                _stakeLastProcessedBlock =
                    BigInteger.Max(_stakeLastProcessedBlock, latestBlock);

                _logger.LogInformation(
                    "{Prefix} Polling completed until block {LatestBlock}",
                    StakeLogPrefix,
                    latestBlock);
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
                    }
                );


                lock (_blockLock)
                {
                    _stakeLastProcessedBlock = BigInteger.Max(
                        _stakeLastProcessedBlock,
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
                    "{prefix} EarlyWithdrawn | DepositId: {DepositId}, Depositor: {Depositor}",
                    StakeLogPrefix,
                    depositId,
                    eLog.Event.Depositor
                );

                SentrySdk.CaptureMessage(
                    $"{StakeLogPrefix} EarlyWithdrawn | DepositId: {depositId}, Depositor: {eLog.Event.Depositor}"
                );

                await _transactionLogService.CreateEarlyWithdrawnLogAsync(
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
                        Network = NetworkName
                    }
                );

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
                    }
                );


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
                    }
                );


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
                    await _transactionLogService.GetDepositLastCheckedBlockNumberAsync(NetworkName);

                lock (_blockLock)
                {
                    _stakeLastProcessedBlock = lastDbBlock;
                }

                if (_stakeLastProcessedBlock > 0)
                    return _stakeLastProcessedBlock.ToHexBigInteger();

                try
                {


                    var latestBlockNumber = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

                    lock (_blockLock)
                    {
                        _stakeLastProcessedBlock = latestBlockNumber;
                        return latestBlockNumber;
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
            _logger.LogInformation("Shutting down blockchain Polling service...");
            await base.StopAsync(cancellationToken);
        }
    }
}
