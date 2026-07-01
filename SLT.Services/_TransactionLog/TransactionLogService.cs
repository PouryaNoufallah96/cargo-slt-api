using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Linq;
using Nethereum.Web3;
using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using SLT.Services._BlockChain;
using SLT.Services._Order;
using SLT.Services._Order.DTOs.Results;
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

                await _transactionLogRepository.InsertOneAsync(newLog);

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

                await _transactionLogRepository.InsertOneAsync(newLog);

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
                var existsLog = await _transactionLogRepository.AsQueryable()
                    .Where(q =>
                        q.Hash.ToLower() == log.Hash.ToLower() &&
                        q.InvoiceId.ToLower() == log.InvoiceId.ToLower() &&
                        q.EventType == BlockchainEventType.LockedInvoiceCreated)
                    .FirstOrDefaultAsync();

                if (existsLog != null)
                {
                    _logger.LogWarning(
                        "Duplicate LockedInvoiceCreated log detected for InvoiceId {InvoiceId}. Skipping insertion. Hash: {Hash}",
                        log.InvoiceId, log.Hash);
                }
                else
                {
                    var tokenData = _availableTokenSetting.FirstOrDefault(q =>
                        !string.IsNullOrWhiteSpace(log.Token) &&
                        q.Address.ToLower() == log.Token.ToLower() &&
                        q.Network.ToLower() == log.Network.ToLower());

                    var amount = tokenData != null
                        ? _blockChainService.ConvertFromWei(log.UsdAmount, tokenData.PriceDecimalPlaces)
                        : Web3.Convert.FromWei(log.UsdAmount);

                    var newLog = new TransactionLog
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
                    };

                    await _transactionLogRepository.InsertOneAsync(newLog);
                }

                var syncResult = await _orderService.SyncLockedInvoiceCreatedAsync(log);

                if (syncResult != null && syncResult.Changed)
                {
                    try
                    {
                        var shortInvoiceId = log.InvoiceId.Length > 10 ? log.InvoiceId[..10] : log.InvoiceId;
                        await _hubContext.Clients.Group(log.Creator).SendAsync("PaymentMessage", $"Locked invoice {shortInvoiceId} created.");
                    }
                    catch (Exception)
                    {
                        _logger.LogError("Failed to send payment notification for InvoiceId {InvoiceId} to wallet {Wallet}.", log.InvoiceId, log.Creator);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating LockedInvoiceCreated transaction log.");
            }
        }

        public async Task CreateLockedInvoicePaidAsync(LockedInvoicePaidLog log)
        {
            try
            {
                var existsLog = await _transactionLogRepository.AsQueryable()
                    .Where(q =>
                        q.Hash.ToLower() == log.Hash.ToLower() &&
                        q.InvoiceId.ToLower() == log.InvoiceId.ToLower() &&
                        q.EventType == BlockchainEventType.LockedInvoicePaid)
                    .FirstOrDefaultAsync();

                if (existsLog != null)
                {
                    _logger.LogWarning(
                        "Duplicate LockedInvoicePaid log detected for InvoiceId {InvoiceId}. Skipping insertion. Hash: {Hash}",
                        log.InvoiceId, log.Hash);
                }
                else
                {
                    var tokenData = _availableTokenSetting.FirstOrDefault(q =>
                        !string.IsNullOrWhiteSpace(log.Token) &&
                        q.Address.ToLower() == log.Token.ToLower() &&
                        q.Network.ToLower() == log.Network.ToLower());

                    var amount = tokenData != null
                        ? _blockChainService.ConvertFromWei(log.PayAmount, tokenData.PriceDecimalPlaces)
                        : Web3.Convert.FromWei(log.PayAmount);

                    var newLog = new TransactionLog
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
                    };

                    await _transactionLogRepository.InsertOneAsync(newLog);
                }

                var syncResult = await _orderService.SyncLockedInvoicePaidAsync(log);

                if (syncResult != null && syncResult.Changed)
                {
                    try
                    {
                        var shortInvoiceId = log.InvoiceId.Length > 10 ? log.InvoiceId[..10] : log.InvoiceId;

                        if (!string.IsNullOrWhiteSpace(syncResult.OwnerWallet))
                            await _hubContext.Clients.Group(syncResult.OwnerWallet).SendAsync("PaymentMessage", $"Locked invoice {shortInvoiceId} funded.");

                        if (!string.IsNullOrWhiteSpace(syncResult.PayerWallet))
                            await _hubContext.Clients.Group(syncResult.PayerWallet).SendAsync("PaymentMessage", $"You funded locked invoice {shortInvoiceId}.");
                    }
                    catch (Exception)
                    {
                        _logger.LogError("Failed to send payment notification for InvoiceId {InvoiceId}.", log.InvoiceId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating LockedInvoicePaid transaction log.");
            }
        }

        public async Task CreateLockedInvoiceApprovedAsync(LockedInvoiceApprovedLog log)
        {
            try
            {
                var existsLog = await _transactionLogRepository.AsQueryable()
                    .Where(q =>
                        q.Hash.ToLower() == log.Hash.ToLower() &&
                        q.InvoiceId.ToLower() == log.InvoiceId.ToLower() &&
                        q.EventType == BlockchainEventType.LockedInvoiceApproved)
                    .FirstOrDefaultAsync();

                if (existsLog != null)
                {
                    _logger.LogWarning(
                        "Duplicate LockedInvoiceApproved log detected for InvoiceId {InvoiceId}. Skipping insertion. Hash: {Hash}",
                        log.InvoiceId, log.Hash);
                }
                else
                {
                    var newLog = new TransactionLog
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
                    };

                    await _transactionLogRepository.InsertOneAsync(newLog);
                }

                var syncResult = await _orderService.SyncLockedInvoiceApprovedAsync(log);

                if (syncResult != null && syncResult.Changed)
                {
                    try
                    {
                        var shortInvoiceId = log.InvoiceId.Length > 10 ? log.InvoiceId[..10] : log.InvoiceId;

                        if (!string.IsNullOrWhiteSpace(syncResult.OwnerWallet))
                            await _hubContext.Clients.Group(syncResult.OwnerWallet).SendAsync("PaymentMessage", $"Locked invoice {shortInvoiceId} approved.");

                        if (!string.IsNullOrWhiteSpace(syncResult.PayerWallet))
                            await _hubContext.Clients.Group(syncResult.PayerWallet).SendAsync("PaymentMessage", $"Locked invoice {shortInvoiceId} approved.");

                        if (!string.IsNullOrWhiteSpace(syncResult.ApproverWallet))
                            await _hubContext.Clients.Group(syncResult.ApproverWallet).SendAsync("PaymentMessage", $"Locked invoice {shortInvoiceId} approved.");
                    }
                    catch (Exception)
                    {
                        _logger.LogError("Failed to send payment notification for InvoiceId {InvoiceId}.", log.InvoiceId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating LockedInvoiceApproved transaction log.");
            }
        }

        public async Task CreateLockedInvoiceResolvedAsync(LockedInvoiceResolvedLog log)
        {
            try
            {
                var existsLog = await _transactionLogRepository.AsQueryable()
                    .Where(q =>
                        q.Hash.ToLower() == log.Hash.ToLower() &&
                        q.InvoiceId.ToLower() == log.InvoiceId.ToLower() &&
                        q.EventType == BlockchainEventType.LockedInvoiceResolved)
                    .FirstOrDefaultAsync();

                if (existsLog != null)
                {
                    _logger.LogWarning(
                        "Duplicate LockedInvoiceResolved log detected for InvoiceId {InvoiceId}. Skipping insertion. Hash: {Hash}",
                        log.InvoiceId, log.Hash);
                }
                else
                {
                    var tokenData = _availableTokenSetting.FirstOrDefault(q =>
                        q.Network.ToLower() == log.Network.ToLower());

                    var amount = tokenData != null
                        ? _blockChainService.ConvertFromWei(log.Amount, tokenData.PriceDecimalPlaces)
                        : Web3.Convert.FromWei(log.Amount);

                    var newLog = new TransactionLog
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
                    };

                    await _transactionLogRepository.InsertOneAsync(newLog);
                }

                var syncResult = await _orderService.SyncLockedInvoiceResolvedAsync(log);

                if (syncResult != null && syncResult.Changed)
                {
                    try
                    {
                        var shortInvoiceId = log.InvoiceId.Length > 10 ? log.InvoiceId[..10] : log.InvoiceId;
                        var transition = syncResult.LockState == LockState.Refunded ? "refunded" : "released";

                        if (!string.IsNullOrWhiteSpace(syncResult.OwnerWallet))
                            await _hubContext.Clients.Group(syncResult.OwnerWallet).SendAsync("PaymentMessage", $"Locked invoice {shortInvoiceId} {transition}.");

                        if (!string.IsNullOrWhiteSpace(syncResult.PayerWallet))
                            await _hubContext.Clients.Group(syncResult.PayerWallet).SendAsync("PaymentMessage", $"Locked invoice {shortInvoiceId} {transition}.");

                        if (!string.IsNullOrWhiteSpace(syncResult.BeneficiaryWallet))
                            await _hubContext.Clients.Group(syncResult.BeneficiaryWallet).SendAsync("PaymentMessage", $"Locked invoice {shortInvoiceId} {transition}.");
                    }
                    catch (Exception)
                    {
                        _logger.LogError("Failed to send payment notification for InvoiceId {InvoiceId}.", log.InvoiceId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating LockedInvoiceResolved transaction log.");
            }
        }


        /// <summary>
        /// use to get last checked block number for transaction confirmation
        /// </summary>
        /// <returns></returns>
        public async Task<BigInteger> GetInvoiceLastCheckedBlockNumberAsync(string network)
        {
            var lastBlock = await _transactionLogRepository
             .AsQueryable()
             .Where(q => q.Network == network)
             .Where(h => h.EventType == BlockchainEventType.InvoiceCreated)
             .OrderByDescending(b => b)
             .FirstOrDefaultAsync();
            if (lastBlock == null)
            {
                return BigInteger.Zero;
            }

            return new BigInteger(lastBlock.BlockNumber);
        }

        public async Task<BigInteger> GetCombinedInvoiceEventLastCheckedBlockNumberAsync(string network)
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
                return BigInteger.Zero;

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

                await _transactionLogRepository.InsertOneAsync(newLog);
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

                await _transactionLogRepository.InsertOneAsync(newLog);
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

                await _transactionLogRepository.InsertOneAsync(newLog);
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

        private T? DeserializeData<T>(string data)
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
