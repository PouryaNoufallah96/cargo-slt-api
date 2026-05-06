using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Linq;
using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using SLT.Services._Order;
using SLT.Services._TransactionLog._Hub;
using SLT.Services._TransactionLog.DTOs;
using System.Numerics;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._TransactionLog
{
    public class TransactionLogService(ITransactionLogRepository _transactionLogRepository,
        IHubContext<WalletNotifyHub> _hubContext,
        IOrderService _orderService,
        ILogger<TransactionLogService> _logger) : ITransactionLogService, IScopedDependency
    {

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



        /// <summary>
        /// use to get last checked block number for transaction confirmation
        /// </summary>
        /// <returns></returns>
        public async Task<BigInteger> GetLastCheckedBlockNumberAsync()
        {
            var lastBlock = await _transactionLogRepository
             .AsQueryable()
             .Where(h => h.EventType == BlockchainEventType.TransactionConfirmed)
             .OrderByDescending(b => b)
             .FirstOrDefaultAsync();
            if(lastBlock == null)
            {
                return BigInteger.Zero;
            }

            return new BigInteger(lastBlock.BlockNumber);
        }

    }
}
