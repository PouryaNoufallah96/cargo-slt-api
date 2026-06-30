using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nethereum.Web3;
using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using SLT.Services._BlockChain;
using SLT.Services._Order;
using SLT.Services._Price.DTOs.Settings;
using SLT.Services._Stake;
using SLT.Services._TransactionLog._Hub;
using SLT.Services._TransactionLog.DTOs;
using SLT.Services._Withdrawal;
using System.Numerics;
using System.Text.Json;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._TransactionLog
{
    public class TransactionLogService(ITransactionLogRepository _transactionLogRepository,
        IStakeService _stakeService,
        IHubContext<WalletNotifyHub> _hubContext,
        IWithdrawalService _withdrawalService,
        IOrderService _orderService,
        IBlockChainService _blockChainService,
        AvailableTokensSettings _availableTokenSetting,
        IInvoiceRepository _invoiceRepository,
        ILogger<TransactionLogService> _logger) : ITransactionLogService, IScopedDependency
    {


        #region Invoice
        /// <summary>
        /// use for creating invoice created log
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        public async Task CreateInvoiceCreatedAsync(InvoiceCreatedLog log)
        {
            try
            {
                var existsLog = await _transactionLogRepository.AsQueryable()
                    .Where(q =>
                        q.Hash.ToLower() == log.Hash.ToLower() &&
                        q.InvoiceId.ToLower() == log.InvoiceId.ToLower() &&
                        q.EventType == BlockchainEventType.InvoiceCreated)
                    .FirstOrDefaultAsync();

                var invoiceId = log.InvoiceId;
                var txHash = log.Hash;
                var owner = log.Creator;

                if (existsLog != null)
                {
                    _logger.LogWarning(
                        "Duplicate InvoiceCreated log detected for InvoiceId {InvoiceId}. Skipping insertion. Hash: {Hash}",
                        log.InvoiceId, log.Hash);
                    return;
                }

                var now = DateTime.UtcNow;

                var newLog = new TransactionLog
                {
                    EventType = BlockchainEventType.InvoiceCreated,
                    InvoiceId = invoiceId,
                    BlockNumber = (decimal)log.BlockNumber,
                    Hash = txHash,
                    Amount = log.UsdAmount,
                    UnlockTime = log.UnLockTime ?? null,
                    Status = TransactionStatus.Confirmed,
                    Wallet = owner,
                    TokenAddress = log.Address,
                    Network = log.Network,

                };

                if (!await TryInsertTransactionLogAsync(newLog))
                    return;

                await _orderService.ActivateNotRegisteredInvoiceAsync(invoiceId, txHash);

                try
                {
                    var shortInvoiceId = log.InvoiceId.Length > 10 ? txHash[..10] : txHash;
                    await _hubContext.Clients.Group(owner).SendAsync("PaymentMessage", $"Invoice Created : {shortInvoiceId}");
                }
                catch (Exception)
                {
                    _logger.LogError("Failed to send payment notification for InvoiceId {InvoiceId} to wallet {Wallet}.", log.InvoiceId, owner);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating InvoiceCreated transaction log.");
            }
        }


        /// <summary>
        /// use for creating invoice paid log
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        public async Task CreateInvoicePaidAsync(InvoicePaidLog log)
        {
            try
            {
                var existsLog = await _transactionLogRepository.AsQueryable()
                    .Where(q =>
                        q.Hash.ToLower() == log.Hash.ToLower() &&
                        q.InvoiceId.ToLower() == log.InvoiceId.ToLower() &&
                        q.EventType == BlockchainEventType.InvoicePaid)
                    .FirstOrDefaultAsync();

                if (existsLog != null)
                {
                    _logger.LogWarning(
                        "Duplicate InvoicePaid log detected for InvoiceId {InvoiceId}. Skipping insertion. Hash: {Hash}",
                        log.InvoiceId, log.Hash);
                    return;
                }


                var newLog = new TransactionLog
                {
                    EventType = BlockchainEventType.InvoicePaid,
                    InvoiceId = log.InvoiceId,
                    BlockNumber = (decimal)log.BlockNumber,
                    Hash = log.Hash,
                    Amount = log.PayAmount,
                    UnlockTime = null,
                    Status = TransactionStatus.Confirmed,
                    Wallet = log.Address,
                    TokenAddress = log.Address,
                    Network = log.Network
                };

                if (!await TryInsertTransactionLogAsync(newLog))
                    return;

                var txHash = log.Hash;
                var ownerWallet = await _orderService.SyncPaidInvoiceAsync(log.InvoiceId, log.Payer, txHash);
                var now = DateTime.UtcNow;


                try
                {
                    var shortInvoiceId = log.InvoiceId.Length > 10 ? txHash[..10] : txHash;
                    await _hubContext.Clients.Group(ownerWallet).SendAsync("PaymentMessage", $"Your Invoice {shortInvoiceId}... has been successfully Paid.");
                    await _hubContext.Clients.Group(log.Payer).SendAsync("PaymentMessage", $"You successfully paid Invoice {shortInvoiceId}...");
                }
                catch (Exception)
                {
                    _logger.LogError("Failed to send payment notification for InvoiceId {InvoiceId} to wallet {Wallet}.", log.InvoiceId, log.Payer);
                }


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating InvoicePaid transaction log.");
            }
        }

        public async Task CreateLockedInvoiceCreatedAsync(LockedInvoiceCreatedLog log)
        {
            try
            {
                var amount = await ConvertLockedTokenAmountAsync(log.InvoiceId, log.Token, log.Network, log.UsdAmount);

                await CreateLockedInvoiceLogAsync(
                    new TransactionLog
                    {
                        EventType = BlockchainEventType.LockedInvoiceCreated,
                        InvoiceId = log.InvoiceId,
                        BlockNumber = (decimal)log.BlockNumber,
                        Hash = log.Hash,
                        Amount = amount,
                        UnlockTime = log.UnLockTime,
                        Status = TransactionStatus.Confirmed,
                        Wallet = log.Creator,
                        TokenAddress = log.Token,
                        Network = log.Network,
                        Data = JsonSerializer.Serialize(new
                        {
                            log.InvoiceId,
                            log.Creator,
                            log.Token,
                            UsdAmount = log.UsdAmount.ToString(),
                            log.UnLockTime,
                            LockDuration = log.LockDuration.ToString(),
                            log.Approver
                        })
                    });

                var syncResult = await _orderService.SyncLockedInvoiceCreatedAsync(log);
                await NotifyLockedInvoiceTransitionAsync(syncResult, "created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating LockedInvoiceCreated transaction log.");
                throw;
            }
        }

        public async Task CreateLockedInvoicePaidAsync(LockedInvoicePaidLog log)
        {
            try
            {
                var amount = await ConvertLockedTokenAmountAsync(log.InvoiceId, log.Token, log.Network, log.PayAmount);

                await CreateLockedInvoiceLogAsync(
                    new TransactionLog
                    {
                        EventType = BlockchainEventType.LockedInvoicePaid,
                        InvoiceId = log.InvoiceId,
                        BlockNumber = (decimal)log.BlockNumber,
                        Hash = log.Hash,
                        Amount = amount,
                        UnlockTime = log.LockedUntil,
                        Status = TransactionStatus.Confirmed,
                        Wallet = log.Payer,
                        TokenAddress = log.Token,
                        Network = log.Network,
                        Data = JsonSerializer.Serialize(new
                        {
                            log.InvoiceId,
                            log.Payer,
                            log.Token,
                            PayAmount = log.PayAmount.ToString(),
                            log.LockedUntil
                        })
                    });

                var syncResult = await _orderService.SyncLockedInvoicePaidAsync(log);
                await NotifyLockedInvoiceTransitionAsync(syncResult, "funded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating LockedInvoicePaid transaction log.");
                throw;
            }
        }

        public async Task CreateLockedInvoiceApprovedAsync(LockedInvoiceApprovedLog log)
        {
            try
            {
                await CreateLockedInvoiceLogAsync(
                    new TransactionLog
                    {
                        EventType = BlockchainEventType.LockedInvoiceApproved,
                        InvoiceId = log.InvoiceId,
                        BlockNumber = (decimal)log.BlockNumber,
                        Hash = log.Hash,
                        Amount = 0,
                        UnlockTime = null,
                        Status = TransactionStatus.Confirmed,
                        Wallet = log.Approver,
                        TokenAddress = log.Address,
                        Network = log.Network,
                        Data = JsonSerializer.Serialize(new
                        {
                            log.InvoiceId,
                            log.Approver
                        })
                    });

                var syncResult = await _orderService.SyncLockedInvoiceApprovedAsync(log);
                await NotifyLockedInvoiceTransitionAsync(syncResult, "approved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating LockedInvoiceApproved transaction log.");
                throw;
            }
        }

        public async Task CreateLockedInvoiceResolvedAsync(LockedInvoiceResolvedLog log)
        {
            try
            {
                var amount = await ConvertLockedTokenAmountAsync(log.InvoiceId, null, log.Network, log.Amount);

                await CreateLockedInvoiceLogAsync(
                    new TransactionLog
                    {
                        EventType = BlockchainEventType.LockedInvoiceResolved,
                        InvoiceId = log.InvoiceId,
                        BlockNumber = (decimal)log.BlockNumber,
                        Hash = log.Hash,
                        Amount = amount,
                        UnlockTime = null,
                        Status = TransactionStatus.Confirmed,
                        Wallet = log.Beneficiary,
                        TokenAddress = log.Address,
                        Network = log.Network,
                        Data = JsonSerializer.Serialize(new
                        {
                            log.InvoiceId,
                            log.Beneficiary,
                            Amount = log.Amount.ToString(),
                            FeeAmount = log.FeeAmount.ToString()
                        })
                    });

                var syncResult = await _orderService.SyncLockedInvoiceResolvedAsync(log);
                await NotifyLockedInvoiceTransitionAsync(syncResult, syncResult.LockState == LockState.Refunded ? "refunded" : "released");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating LockedInvoiceResolved transaction log.");
                throw;
            }
        }

        private async Task<bool> CreateLockedInvoiceLogAsync(TransactionLog transactionLog)
        {
            return await TryInsertTransactionLogAsync(transactionLog);
        }

        private async Task<bool> TryInsertTransactionLogAsync(TransactionLog transactionLog)
        {
            var existsLog = await _transactionLogRepository.AsQueryable()
                .Where(q =>
                    q.Hash.ToLower() == transactionLog.Hash.ToLower() &&
                    q.InvoiceId.ToLower() == transactionLog.InvoiceId.ToLower() &&
                    q.EventType == transactionLog.EventType)
                .FirstOrDefaultAsync();

            if (existsLog != null)
            {
                LogDuplicateTransactionLog(transactionLog);
                return false;
            }

            try
            {
                await _transactionLogRepository.InsertOneAsync(transactionLog);
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                LogDuplicateTransactionLog(transactionLog);
                return false;
            }
        }

        private void LogDuplicateTransactionLog(TransactionLog transactionLog)
        {
            _logger.LogWarning(
                "Duplicate {EventType} log detected for InvoiceId {InvoiceId}. Skipping insertion. Hash: {Hash}",
                transactionLog.EventType, transactionLog.InvoiceId, transactionLog.Hash);
        }

        private async Task<decimal> ConvertLockedTokenAmountAsync(string invoiceId, string tokenAddress, string network, BigInteger amount)
        {
            var tokenData = _availableTokenSetting.FirstOrDefault(q =>
                AddressEquals(q.Address, tokenAddress) &&
                string.Equals(q.Network, network, StringComparison.OrdinalIgnoreCase));

            if (tokenData == null && !string.IsNullOrWhiteSpace(invoiceId))
            {
                var invoice = await _invoiceRepository.AsQueryable()
                    .Where(q => q.InvoiceId.ToLower() == invoiceId.ToLower())
                    .FirstOrDefaultAsync();

                if (invoice != null)
                {
                    tokenData = _availableTokenSetting.FirstOrDefault(q =>
                        string.Equals(q.Name, invoice.TokenSymbol, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (tokenData == null)
            {
                _logger.LogWarning(
                    "Could not resolve token decimals for locked invoice amount. InvoiceId: {InvoiceId}, TokenAddress: {TokenAddress}, Network: {Network}",
                    invoiceId,
                    tokenAddress,
                    network);
                return Web3.Convert.FromWei(amount);
            }

            return _blockChainService.ConvertFromWei(amount, tokenData.PriceDecimalPlaces);
        }

        private async Task NotifyLockedInvoiceTransitionAsync(LockedInvoiceSyncResult syncResult, string transition)
        {
            if (syncResult == null || !syncResult.Changed)
                return;

            SentrySdk.AddBreadcrumb(
                $"Locked invoice {transition} synced. InvoiceId: {syncResult.InvoiceId}",
                "locked-invoice");

            var wallets = new[]
                {
                    syncResult.OwnerWallet,
                    syncResult.PayerWallet,
                    syncResult.ApproverWallet,
                    syncResult.BeneficiaryWallet
                }
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!wallets.Any())
                return;

            var shortInvoiceId = !string.IsNullOrEmpty(syncResult.InvoiceId) && syncResult.InvoiceId.Length > 10
                ? syncResult.InvoiceId[..10]
                : syncResult.InvoiceId;

            try
            {
                foreach (var wallet in wallets)
                {
                    await _hubContext.Clients.Group(wallet)
                        .SendAsync("PaymentMessage", $"Locked invoice {shortInvoiceId} {transition}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send locked invoice notification. InvoiceId: {InvoiceId}", syncResult.InvoiceId);
            }
        }

        private static bool AddressEquals(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// use to get last checked block number for transaction confirmation
        /// </summary>
        /// <returns></returns>
        public async Task<BigInteger> GetInvoiceLastCheckedBlockNumberAsync(string network)
        {
            var invoiceEventTypes = new[]
            {
                BlockchainEventType.InvoiceCreated,
                BlockchainEventType.InvoicePaid,
                BlockchainEventType.LockedInvoiceCreated,
                BlockchainEventType.LockedInvoicePaid,
                BlockchainEventType.LockedInvoiceApproved,
                BlockchainEventType.LockedInvoiceResolved
            };

            var lastBlock = await _transactionLogRepository
             .AsQueryable()
             .Where(q => q.Network == network)
             .Where(h => invoiceEventTypes.Contains(h.EventType))
             .OrderByDescending(b => b.BlockNumber)
             .FirstOrDefaultAsync();
            if (lastBlock == null)
            {
                return BigInteger.Zero;
            }

            var overlapBlocks = new BigInteger(10);
            var fromBlock = new BigInteger(lastBlock.BlockNumber) - overlapBlocks;
            return BigInteger.Max(fromBlock, BigInteger.Zero);
        }
        #endregion



        #region Stake

        public async Task CreateDepositCreatedLogAsync(DepositCreatedLog input)
        {
            try
            {
                var existsLog = await _transactionLogRepository.AsQueryable()
                    .Where(q =>
                        q.Hash.ToLower() == input.Hash.ToLower() &&
                        q.InvoiceId.ToLower() == input.DepositId.ToLower() &&
                        q.EventType == BlockchainEventType.DepositCreated)
                    .FirstOrDefaultAsync();

                if (existsLog != null)
                {
                    _logger.LogWarning(
                        "Duplicate DepositCreated log detected for DepositId {DepositId}. Skipping insertion. Hash: {Hash}",
                        input.DepositId, input.Hash);
                    return;
                }

                var newLog = new TransactionLog
                {
                    Hash = input.Hash,
                    Wallet = input.Depositor,
                    BlockNumber = (decimal)input.BlockNumber,
                    EventType = BlockchainEventType.DepositCreated,
                    Status = TransactionStatus.Confirmed,
                    Network = input.Network,
                    InvoiceId = input.DepositId,
                    TokenAddress = input.Token,
                    Data = SerializeData(input)
                };

                if (!await TryInsertTransactionLogAsync(newLog))
                    return;

                await _stakeService.ActivateStakeAsync(input.DepositId, input.Hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating DepositCreated transaction log.");
            }
        }

        //public async Task CreateEarlyWithdrawnLogAsync(EarlyWithdrawnLog input)
        //{
        //    try
        //    {
        //        var existsLog = await _transactionLogRepository.AsQueryable()
        //            .Where(q =>
        //                q.Hash.ToLower() == input.Hash.ToLower() &&
        //                q.InvoiceId.ToLower() == input.DepositId.ToLower() &&
        //                q.EventType == BlockchainEventType.EarlyWithdrawn)
        //            .FirstOrDefaultAsync();

        //        if (existsLog != null)
        //        {
        //            _logger.LogWarning(
        //                "Duplicate EarlyWithdrawn log detected for DepositId {DepositId}. Skipping insertion. Hash: {Hash}",
        //                input.DepositId, input.Hash);
        //            return;
        //        }

        //        var newLog = new TransactionLog
        //        {
        //            Hash = input.Hash,
        //            Wallet = input.Depositor,
        //            BlockNumber = (decimal)input.BlockNumber,
        //            EventType = BlockchainEventType.EarlyWithdrawn,
        //            Status = TransactionStatus.Confirmed,
        //            Network = input.Network,

        //            InvoiceId = input.DepositId,
        //            Data = SerializeData(input)
        //        };

        //        await _transactionLogRepository.InsertOneAsync(newLog);
        //        await _withdrawalService.CreateEarlyWithdrawnByEventAsync(input.DepositId, input.Hash, input.WithdrawAmount, input.ProfitAmount, input.ClaimedProfitAmount);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error while creating EarlyWithdrawn transaction log.");
        //    }
        //}

        public async Task CreateProfitWithdrawnLogAsync(ProfitWithdrawnLog input)
        {
            try
            {
                var existsLog = await _transactionLogRepository.AsQueryable()
                    .Where(q =>
                        q.Hash.ToLower() == input.Hash.ToLower() &&
                        q.InvoiceId.ToLower() == input.DepositId.ToLower() &&
                        q.EventType == BlockchainEventType.ProfitWithdrawn)
                    .FirstOrDefaultAsync();

                if (existsLog != null)
                {
                    _logger.LogWarning(
                        "Duplicate ProfitWithdrawn log detected for DepositId {DepositId}. Skipping insertion. Hash: {Hash}",
                        input.DepositId, input.Hash);
                    return;
                }

                var newLog = new TransactionLog
                {
                    Hash = input.Hash,
                    Wallet = input.Depositor,
                    BlockNumber = (decimal)input.BlockNumber,
                    EventType = BlockchainEventType.ProfitWithdrawn,
                    Status = TransactionStatus.Confirmed,
                    Network = input.Network,

                    InvoiceId = input.DepositId,
                    TokenAddress = input.Token,

                    Data = SerializeData(input)
                };

                if (!await TryInsertTransactionLogAsync(newLog))
                    return;

                await _withdrawalService.CreateProfitWithdrawaByEventAsycn(input.DepositId, input.Profit, input.Hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating ProfitWithdrawn transaction log.");
            }
        }

        public async Task CreateWithdrawnLogAsync(WithdrawnLog input)
        {
            try
            {
                var existsLog = await _transactionLogRepository.AsQueryable()
                    .Where(q =>
                        q.Hash.ToLower() == input.Hash.ToLower() &&
                        q.InvoiceId.ToLower() == input.DepositId.ToLower() &&
                        q.EventType == BlockchainEventType.WithdrawnAll)
                    .FirstOrDefaultAsync();

                if (existsLog != null)
                {
                    _logger.LogWarning(
                        "Duplicate Withdrawn log detected for DepositId {DepositId}. Skipping insertion. Hash: {Hash}",
                        input.DepositId, input.Hash);
                    return;
                }

                var newLog = new TransactionLog
                {
                    Hash = input.Hash,
                    Wallet = input.Depositor,
                    BlockNumber = (decimal)input.BlockNumber,
                    EventType = BlockchainEventType.WithdrawnAll,
                    Status = TransactionStatus.Confirmed,
                    Network = input.Network,

                    InvoiceId = input.DepositId,

                    Data = SerializeData(input)
                };

                if (!await TryInsertTransactionLogAsync(newLog))
                    return;

                await _withdrawalService.CreateWithdrawnAllByEventAsync(input.DepositId, input.Hash, input.Principal, input.Profit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating Withdrawn transaction log.");
            }
        }

        public async Task<BigInteger> GetDepositLastCheckedBlockNumberAsync(string network = "BEP20")
        {
            var lastBlock = await _transactionLogRepository
                .AsQueryable()
                .Where(h =>
                    h.Network == network &&
                    (
                        h.EventType == BlockchainEventType.DepositCreated
                    ))
                .OrderByDescending(b => b.BlockNumber)
                .FirstOrDefaultAsync();

            if (lastBlock == null)
            {
                return BigInteger.Zero;
            }

            return new BigInteger(lastBlock.BlockNumber);
        }

        #endregion


        private string SerializeData<T>(T input)
        {
            object data = input switch
            {
                DepositCreatedLog x => new DepositCreatedData
                {
                    Depositor = x.Depositor.ToString(),
                    UnlocksAt = x.UnlocksAt.ToString(),
                    Profit = x.Profit.ToString(),
                    Principal = x.Principal.ToString(),
                    LockDuration = x.LockDuration.ToString()
                },

                EarlyWithdrawnLog x => new EarlyWithdrawnData
                {
                    Depositor = x.Depositor,
                    ClaimedProfitAmount = x.ClaimedProfitAmount.ToString(),
                    FinalPayoutAmount = x.FinalPayoutAmount.ToString(),
                    ProfitAmount = x.ProfitAmount.ToString(),
                    WithdrawAmount = x.WithdrawAmount.ToString()
                },

                ProfitWithdrawnLog x => new ProfitWithdrawnData
                {
                    Depositor = x.Depositor,
                    Profit = x.Profit.ToString()
                },

                WithdrawnLog x => new WithdrawnData
                {
                    Depositor = x.Depositor,
                    Principal = x.Principal.ToString(),
                    Profit = x.Profit.ToString(),
                    TotalPayout = x.TotalPayout.ToString()
                },

                _ => throw new NotSupportedException($"No serializer defined for type {typeof(T).Name}")
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        private T DeserializeData<T>(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return default;

            return JsonSerializer.Deserialize<T>(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

    }
}
