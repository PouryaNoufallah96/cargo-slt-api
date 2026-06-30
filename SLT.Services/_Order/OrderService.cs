using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nethereum.Contracts.Standards.ERC20.TokenList;
using Nethereum.Util;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Utilities;
using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using SLT.Services._BlockChain;
using SLT.Services._BlockChain.DTOs.Updates;
using SLT.Services._Order.DTOs.Results;
using SLT.Services._Order.DTOs.Updates;
using SLT.Services._Price.DTOs.Settings;
using SLT.Services._TransactionLog.DTOs;
using System.Numerics;
using System.Security.Cryptography;
using Utilities.Enums;
using Utilities.Exceptions.Common;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._Order
{
    public class OrderService(
        AvailableTokensSettings _availableTokenSetting,
        ILogger<OrderService> _logger,
        IOrderRepository _orderRepository,
        IBlockChainService _blockChainService,
        IInvoiceRepository _invoiceRepository,
        IRandomService _randomService) : IOrderService, IScopedDependency
    {
        private const string ZeroAddress = "0x0000000000000000000000000000000000000000";
        private static readonly int[] AllowedLockDurations = new[] { 1, 3, 6, 12, 18, 24 };


        /// <summary>
        /// use for create quick order
        /// </summary>
        /// <param name="update"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        public async Task<OrderFullResult> CreatePendingQuickOrderAsync(CreateQuickInvoiceUpdate update, string walletAddress, string network)
        {
            var newOrder = new Order
            {
                OrderId = Guid.NewGuid().ToString("N"),
                OwnerWallet = walletAddress,
                PayerWallet = null,
                SeenBy = [],
                State = OrderState.NotRegistered,
                PaymentDay = null,
                Type = OrderType.Quick,
                Transportation = null,
                TotalAmount = update.Amount,
                TransferId = _randomService.GetSecureAlphaNumericString(12).ToUpper(),

            };

            var tokenData = ValidateToken(update.TokenSymbol);
            if (tokenData.Network != network) throw new BadRequestException($"Please Sign with {tokenData.Network} Network with your wallet");
            var lockDurationMonths = update.IsLocked ? ValidateLockDurationMonths(update.LockDurationMonths) : 0;
            var approverWallet = update.IsLocked
                ? ValidateThirdPartyApprover(update.ThirdPartyApprover, walletAddress)
                : ValidateNormalInvoiceHasNoLockFields(update.LockDurationMonths, update.ThirdPartyApprover);


            await _orderRepository.InsertOneAsync(newOrder);
            try
            {
                var invoiceResult = await CreatePendingQuickInvoiceAsync(
                    newOrder,
                    update.TokenSymbol,
                    update.Description,
                    tokenData,
                    update.IsLocked,
                    lockDurationMonths,
                    approverWallet);
                return ConvertToReslut(new List<InvoiceResult> { invoiceResult }, newOrder, OwnershipType.Owner);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureMessage($"Error creating quick invoice for order ex : {ex.Message}");
                await _orderRepository.DeleteByIdAsync(newOrder.Id);
                throw new BadRequestException("Please try later!");
            }
        }


        /// <summary>
        /// use for create quick invoice
        /// </summary>
        /// <param name="order"></param>
        /// <param name="token"></param>
        /// <param name="desc"></param>
        /// <param name="dateOnly"></param>
        /// <returns></returns>
        private async Task<InvoiceResult> CreatePendingQuickInvoiceAsync(
            Order order,
            string token,
            string desc,
            AvailableTokenData tokenData,
            bool isLocked = false,
            int lockDurationMonths = 0,
            string approverWallet = null,
            DateOnly? dateOnly = null)
        {


            var activeDate = dateOnly.HasValue
                ? dateOnly.Value.ToDateTime(TimeOnly.MinValue)
                : DateOnly.FromDateTime(DateTime.Now)
                    .ToDateTime(TimeOnly.MinValue);

            var newInvoice = new Invoice
            {
                InvoiceId = GenerateBytes32HexId(),
                TokenSymbol = tokenData.Name,
                TokenNetwork = tokenData.Network,
                TokenAddress = tokenData.Address,
                USDTAmount = order.TotalAmount,
                USDTAmountInWei = _blockChainService.ConvertToWei(order.TotalAmount, 18).ToString(),
                OwnerWallet = order.OwnerWallet,
                OrderId = order.OrderId,
                PayerWallet = null,
                State = InvoiceState.NotRegistered,
                ActivateDate = activeDate,
                PayMoment = null,
                Desctiption = desc?.Trim(),
                RegisterHash = null,
                PaymentHash = null,
                Errors = null,
                TokenAmountAtPayment = null,
                TokenAmountWeiAtPayment = null,
                TokenPriceAtPayment = null,
                Lock = CreateLockDetail(isLocked, lockDurationMonths, approverWallet)
            };

            await _invoiceRepository.InsertOneAsync(newInvoice);
            return ConvertToReslut(newInvoice, OwnershipType.Owner);
        }


        public async Task<OrderFullResult> CreatePendingMultiStepOrderAsync(CreateMultiStepOrderUpdate update, string walletAddress, string network)
        {
            if (update == null)
                throw new BadRequestException(nameof(update));

            if (update.Invoices == null || !update.Invoices.Any())
                throw new BadRequestException("Invoices list cannot be empty.");

            var invoicesTotal = update.Invoices.Sum(i => i.Amount);
            if (invoicesTotal != update.TotalAmount)
                throw new BadRequestException(
                    "Sum of invoice amounts does not match order total amount.");

            var approverWallet = update.IsLocked
                ? ValidateThirdPartyApprover(update.ThirdPartyApprover, walletAddress)
                : ValidateNormalMultiStepOrderHasNoLockFields(update);

            foreach (var invoice in update.Invoices)
            {
                var tokenData = ValidateToken(invoice.TokenSymbol);
                if (tokenData.Network != network) throw new BadRequestException($"Please Sign with {tokenData.Network} Network with your wallet");

                if (update.IsLocked)
                    ValidateLockDurationMonths(invoice.LockDurationMonths);
            }


            var newOrder = new Order
            {
                OrderId = Guid.NewGuid().ToString("N"),
                OwnerWallet = walletAddress,
                PayerWallet = null,
                SeenBy = [],
                State = OrderState.NotRegistered,
                PaymentDay = null,
                Type = OrderType.Multi,
                Transportation = update.Transportation.Trim(),
                TotalAmount = update.TotalAmount,
                TransferId = _randomService.GetSecureAlphaNumericString(12).ToUpper(),
            };

            await _orderRepository.InsertOneAsync(newOrder);
            try
            {
                var invoiceResults = await CreatePendingMultiStepInvoicesAsync(
                    newOrder,
                    update.Invoices,
                    update.IsLocked,
                    approverWallet);

                return ConvertToReslut(invoiceResults, newOrder, OwnershipType.Owner);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureMessage($"Error creating multi-step invoices for order ex : {ex.Message}");
                await _orderRepository.DeleteByIdAsync(newOrder.Id);
                throw new BadRequestException("Please try later!");
            }

        }


        /// <summary>
        /// use for create multi step invoices
        /// </summary>
        /// <param name="order"></param>
        /// <param name="invoiceUpdates"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        /// <exception cref="Exception"></exception>
        private async Task<List<InvoiceResult>> CreatePendingMultiStepInvoicesAsync(
        Order order,
        List<MultiStepInvoiceUpdate> invoiceUpdates,
        bool isLocked,
        string approverWallet)
        {
            if (invoiceUpdates == null || !invoiceUpdates.Any())
                throw new BadRequestException("Invoice list is empty.");

            var invoices = new List<Invoice>();

            foreach (var invoiceUpdate in invoiceUpdates)
            {
                var tokenData = ValidateToken(invoiceUpdate.TokenSymbol);

                var nowPlus1 = DateTime.Now.AddMinutes(1);
                var timeOnly = TimeOnly.FromDateTime(nowPlus1);
                var activeDate = invoiceUpdate.ActivationDate.ToDateTime(timeOnly);

                var invoice = new Invoice
                {
                    InvoiceId = GenerateBytes32HexId(),
                    TokenSymbol = tokenData.Name,
                    TokenNetwork = tokenData.Network,
                    TokenAddress = tokenData.Address,
                    USDTAmount = invoiceUpdate.Amount,
                    USDTAmountInWei = _blockChainService
                        .ConvertToWei(invoiceUpdate.Amount, 18)
                        .ToString(),

                    OwnerWallet = order.OwnerWallet,
                    OrderId = order.OrderId,
                    PayerWallet = null,
                    State = InvoiceState.NotRegistered,
                    ActivateDate = activeDate,
                    Desctiption = invoiceUpdate.Description?.Trim(),

                    PayMoment = null,
                    RegisterHash = null,
                    PaymentHash = null,
                    Errors = null,

                    TokenAmountAtPayment = null,
                    TokenAmountWeiAtPayment = null,
                    TokenPriceAtPayment = null,
                    Lock = CreateLockDetail(
                        isLocked,
                        isLocked ? invoiceUpdate.LockDurationMonths.Value : 0,
                        approverWallet)
                };

                invoices.Add(invoice);
            }

            await _invoiceRepository.InsertManyAsync(invoices);

            return invoices.Select(invoice => ConvertToReslut(invoice, OwnershipType.Owner)).ToList();
        }



        public async Task RemoveNotRegisteredOrdersAsync()
        {
            var orders = await _orderRepository.AsQueryable()
                .Where(o => o.State == OrderState.NotRegistered)
                .Take(10)
                .ToListAsync();

            if (!orders.Any())
                return;

            var orderIds = orders.Select(o => o.OrderId).ToList();

            foreach (var order in orders)
            {
                var hasOtherState = await _invoiceRepository.AsQueryable()
                    .AnyAsync(i => i.OrderId == order.OrderId && i.State != InvoiceState.NotRegistered);

                if (hasOtherState)
                    continue;

                await _invoiceRepository.DeleteManyAsync(i => i.OrderId == order.OrderId);

                await _orderRepository.DeleteOneAsync(o => o.Id == order.Id);
            }
        }

        public async Task ActivateNotRegisteredInvoiceAsync(string invoiceId, string hash)
        {
            var invoice = await _invoiceRepository.AsQueryable()
                .Where(q => q.InvoiceId.ToLower() == invoiceId.ToLower())
                .FirstOrDefaultAsync();

            if (invoice == null)
                return;

            if (invoice.Lock != null)
            {
                LogInvoiceModeMismatch(invoiceId, hash, BlockchainEventType.InvoiceCreated, "normal event for locked draft");
                return;
            }

            var filter = Builders<Invoice>.Filter.And(
                Builders<Invoice>.Filter.Eq(x => x.Id, invoice.Id),
                Builders<Invoice>.Filter.Eq(x => x.State, InvoiceState.NotRegistered)
            );

            var update = Builders<Invoice>.Update
                .Set(x => x.State, InvoiceState.Pending)
                .Set(x => x.RegisterHash, hash);

            var options = new FindOneAndUpdateOptions<Invoice>
            {
                ReturnDocument = ReturnDocument.After
            };

            var updatedInvoice = await _invoiceRepository
                .FindOneAndUpdateWithOptionAsync(filter, update, options);

            if (updatedInvoice == null)
                return;

            var orderFilter = Builders<Order>.Filter.And(
                Builders<Order>.Filter.Eq(o => o.OrderId, updatedInvoice.OrderId),
                Builders<Order>.Filter.Eq(o => o.State, OrderState.NotRegistered)
            );

            var orderUpdate = Builders<Order>.Update
                .Set(o => o.State, OrderState.Pending);

            await _orderRepository.FindOneAndUpdateAsync(orderFilter, orderUpdate);
        }





        /// <summary>
        /// use for sync paid invoice
        /// </summary>
        /// <param name="invoiceId"></param>
        /// <param name="payerWallet"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public async Task<string> SyncPaidInvoiceAsync(string invoiceId, string payerWallet, string hash)
        {
            var invoice = await _invoiceRepository.AsQueryable()
                .Where(q => q.InvoiceId.ToLower() == invoiceId.ToLower())
                .FirstOrDefaultAsync();

            if (invoice == null) return null;

            if (invoice.Lock != null)
            {
                LogInvoiceModeMismatch(invoiceId, hash, BlockchainEventType.InvoicePaid, "normal event for locked draft");
                return null;
            }

            if (invoice.State != InvoiceState.Pending) return null;

            invoice.PaymentHash = hash;
            invoice.PayMoment = DateTime.UtcNow;
            invoice.PayerWallet = payerWallet;
            invoice.State = InvoiceState.Completed;
            await _invoiceRepository.ReplaceOneAsync(invoice);

            try
            {
                await SyncOrderWithOrderIdAsync(invoice.OrderId);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }

            return invoice.OwnerWallet;
        }

        public async Task<LockedInvoiceSyncResult> SyncLockedInvoiceCreatedAsync(LockedInvoiceCreatedLog log)
        {
            var invoice = await GetInvoiceForLockedSyncAsync(log.InvoiceId, log.Hash, BlockchainEventType.LockedInvoiceCreated);
            if (invoice == null)
                return new LockedInvoiceSyncResult { Changed = false, InvoiceId = log.InvoiceId };

            var approverWallet = NormalizeZeroAddress(log.Approver);
            var durationMonths = checked((int)log.LockDuration);

            if (invoice.Lock.DurationMonths != durationMonths)
            {
                LogLockedInvoiceDraftMismatch(
                    invoice.InvoiceId,
                    log.Hash,
                    "DurationMonths",
                    invoice.Lock.DurationMonths.ToString(),
                    durationMonths.ToString());
                return BuildLockedSyncResult(invoice, false, null, null, null, invoice.Lock.State, invoice.State);
            }

            if (!OptionalAddressEquals(invoice.Lock.ApproverWallet, approverWallet))
            {
                LogLockedInvoiceDraftMismatch(
                    invoice.InvoiceId,
                    log.Hash,
                    "ApproverWallet",
                    invoice.Lock.ApproverWallet,
                    approverWallet);
                return BuildLockedSyncResult(invoice, false, null, null, null, invoice.Lock.State, invoice.State);
            }

            var changed = await PromoteLockedInvoiceCoarseAsync(invoice);

            var changedFilter = Builders<Invoice>.Filter.Or(
                Builders<Invoice>.Filter.Ne(x => x.RegisterHash, log.Hash)
            );

            var update = Builders<Invoice>.Update
                .Set(x => x.RegisterHash, log.Hash)
                .Max(x => x.Lock.State, LockState.Created);

            changed |= await UpdateLockedInvoiceIfNeededAsync(invoice, changedFilter, update);

            return BuildLockedSyncResult(invoice, changed, null, approverWallet, null, LockState.Created, InvoiceState.Locked);
        }

        public async Task<LockedInvoiceSyncResult> SyncLockedInvoicePaidAsync(LockedInvoicePaidLog log)
        {
            var invoice = await GetInvoiceForLockedSyncAsync(log.InvoiceId, log.Hash, BlockchainEventType.LockedInvoicePaid);
            if (invoice == null)
                return new LockedInvoiceSyncResult { Changed = false, InvoiceId = log.InvoiceId };

            var changed = await PromoteLockedInvoiceCoarseAsync(invoice);
            var payMoment = invoice.PayMoment ?? DateTime.UtcNow;
            var tokenAmount = ConvertInvoiceTokenAmount(invoice, log.PayAmount);
            var tokenAmountWei = log.PayAmount.ToString();

            var changedFilter = Builders<Invoice>.Filter.Or(
                Builders<Invoice>.Filter.Ne(x => x.PayerWallet, log.Payer),
                Builders<Invoice>.Filter.Ne(x => x.PaymentHash, log.Hash),
                Builders<Invoice>.Filter.Eq(x => x.PayMoment, null),
                Builders<Invoice>.Filter.Ne(x => x.TokenAmountAtPayment, tokenAmount),
                Builders<Invoice>.Filter.Ne(x => x.TokenAmountWeiAtPayment, tokenAmountWei),
                Builders<Invoice>.Filter.Ne(x => x.Lock.LockedUntilMoment, log.LockedUntil),
                Builders<Invoice>.Filter.Lt(x => x.Lock.State, LockState.Funded)
            );

            var update = Builders<Invoice>.Update
                .Set(x => x.PayerWallet, log.Payer)
                .Set(x => x.PaymentHash, log.Hash)
                .Set(x => x.PayMoment, payMoment)
                .Set(x => x.TokenAmountAtPayment, tokenAmount)
                .Set(x => x.TokenAmountWeiAtPayment, tokenAmountWei)
                .Set(x => x.Lock.LockedUntilMoment, log.LockedUntil)
                .Max(x => x.Lock.State, LockState.Funded);

            changed |= await UpdateLockedInvoiceIfNeededAsync(invoice, changedFilter, update);

            return BuildLockedSyncResult(invoice, changed, log.Payer, null, null, LockState.Funded, InvoiceState.Locked);
        }

        public async Task<LockedInvoiceSyncResult> SyncLockedInvoiceApprovedAsync(LockedInvoiceApprovedLog log)
        {
            var invoice = await GetInvoiceForLockedSyncAsync(log.InvoiceId, log.Hash, BlockchainEventType.LockedInvoiceApproved);
            if (invoice == null)
                return new LockedInvoiceSyncResult { Changed = false, InvoiceId = log.InvoiceId };

            var changed = await PromoteLockedInvoiceCoarseAsync(invoice);
            var approvedMoment = invoice.Lock.ApprovedMoment ?? DateTime.UtcNow;

            var changedFilter = Builders<Invoice>.Filter.Or(
                Builders<Invoice>.Filter.Ne(x => x.Lock.ApprovedBy, log.Approver),
                Builders<Invoice>.Filter.Eq(x => x.Lock.ApprovedMoment, null),
                Builders<Invoice>.Filter.Ne(x => x.Lock.ApproveHash, log.Hash),
                Builders<Invoice>.Filter.Lt(x => x.Lock.State, LockState.Approved)
            );

            var update = Builders<Invoice>.Update
                .Set(x => x.Lock.ApprovedBy, log.Approver)
                .Set(x => x.Lock.ApprovedMoment, approvedMoment)
                .Set(x => x.Lock.ApproveHash, log.Hash)
                .Max(x => x.Lock.State, LockState.Approved);

            changed |= await UpdateLockedInvoiceIfNeededAsync(invoice, changedFilter, update);

            return BuildLockedSyncResult(invoice, changed, null, log.Approver, null, LockState.Approved, InvoiceState.Locked);
        }

        public async Task<LockedInvoiceSyncResult> SyncLockedInvoiceResolvedAsync(LockedInvoiceResolvedLog log)
        {
            var invoice = await GetInvoiceForLockedSyncAsync(log.InvoiceId, log.Hash, BlockchainEventType.LockedInvoiceResolved);
            if (invoice == null)
                return new LockedInvoiceSyncResult { Changed = false, InvoiceId = log.InvoiceId };

            var released = AddressEquals(log.Beneficiary, invoice.OwnerWallet);
            var refunded = !string.IsNullOrWhiteSpace(invoice.PayerWallet) && AddressEquals(log.Beneficiary, invoice.PayerWallet);

            if (!released && !refunded)
            {
                _logger.LogError(
                    "LockedInvoiceResolved beneficiary mismatch. InvoiceId: {InvoiceId}, Beneficiary: {Beneficiary}, Owner: {Owner}, Payer: {Payer}, Hash: {Hash}",
                    log.InvoiceId,
                    log.Beneficiary,
                    invoice.OwnerWallet,
                    invoice.PayerWallet,
                    log.Hash);
                SentrySdk.CaptureMessage($"LockedInvoiceResolved beneficiary mismatch for InvoiceId {log.InvoiceId}");
                return BuildLockedSyncResult(invoice, false, null, null, log.Beneficiary, invoice.Lock.State, invoice.State);
            }

            var targetLockState = released ? LockState.Released : LockState.Refunded;
            var targetInvoiceState = released ? InvoiceState.Completed : InvoiceState.Refunded;
            var stakedPayout = ConvertInvoiceTokenAmount(invoice, log.Amount);
            var feeAmount = ConvertInvoiceTokenAmount(invoice, log.FeeAmount);
            var stakedPayoutWei = log.Amount.ToString();
            var feeAmountWei = log.FeeAmount.ToString();

            var terminalGuard = Builders<Invoice>.Filter.Nin(
                x => x.Lock.State,
                new[] { LockState.Released, LockState.Refunded });

            var changedFilter = Builders<Invoice>.Filter.Or(
                Builders<Invoice>.Filter.Ne(x => x.State, targetInvoiceState),
                Builders<Invoice>.Filter.Ne(x => x.Lock.BeneficiaryWallet, log.Beneficiary),
                Builders<Invoice>.Filter.Ne(x => x.Lock.StakedPayout, stakedPayout),
                Builders<Invoice>.Filter.Ne(x => x.Lock.StakedPayoutWei, stakedPayoutWei),
                Builders<Invoice>.Filter.Ne(x => x.Lock.FeeAmount, feeAmount),
                Builders<Invoice>.Filter.Ne(x => x.Lock.FeeAmountWei, feeAmountWei),
                Builders<Invoice>.Filter.Ne(x => x.Lock.ResolveHash, log.Hash),
                Builders<Invoice>.Filter.Lt(x => x.Lock.State, targetLockState)
            );

            var update = Builders<Invoice>.Update
                .Set(x => x.State, targetInvoiceState)
                .Set(x => x.Lock.BeneficiaryWallet, log.Beneficiary)
                .Set(x => x.Lock.StakedPayout, stakedPayout)
                .Set(x => x.Lock.StakedPayoutWei, stakedPayoutWei)
                .Set(x => x.Lock.FeeAmount, feeAmount)
                .Set(x => x.Lock.FeeAmountWei, feeAmountWei)
                .Set(x => x.Lock.ResolveHash, log.Hash)
                .Max(x => x.Lock.State, targetLockState);

            var changed = await UpdateLockedInvoiceIfNeededAsync(invoice, changedFilter, update, terminalGuard);

            if (changed)
            {
                await PromoteOrderToPendingAsync(invoice.OrderId);
                await SyncOrderWithOrderIdAsync(invoice.OrderId);
            }

            return BuildLockedSyncResult(invoice, changed, null, null, log.Beneficiary, targetLockState, targetInvoiceState);
        }



        /// <summary>
        /// use for sync transaction log with order
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task SyncOrderWithOrderIdAsync(string orderId)
        {
            var order = await _orderRepository.AsQueryable()
                .Where(q => q.State != OrderState.NotRegistered)
                .Where(q => q.OrderId.ToLower() == orderId.ToLower())
                .FirstOrDefaultAsync();

            if (order == null)
                return;

            var invoices = await _invoiceRepository.AsQueryable()
                .Where(q => q.OrderId.ToLower() == orderId.ToLower())
                .ToListAsync();

            if (!invoices.Any())
                return;

            if (order.Type == OrderType.Quick)
            {
                var hasPaidInvoice = invoices.Any(IsTerminalOrderInvoice);

                if (hasPaidInvoice && order.State != OrderState.Completed)
                {
                    order.State = OrderState.Completed;
                    order.PaymentDay = DateTime.UtcNow;
                    await _orderRepository.ReplaceOneAsync(order);
                }

                return;
            }

            if (order.Type == OrderType.Multi)
            {
                var allInvoicesPaid = invoices.All(IsTerminalOrderInvoice);

                if (allInvoicesPaid && order.State != OrderState.Completed)
                {
                    order.State = OrderState.Completed;
                    order.PaymentDay = DateTime.UtcNow;
                    await _orderRepository.ReplaceOneAsync(order);
                }
            }
        }


        /// <summary>
        /// using for get order list
        /// </summary>
        /// <param name="update"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        public async Task<OrderListResult> GetOrderListAsync(
        GetPendingOrderListUpdate update,
        string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
                throw new BadRequestException("Wallet address is required.");

            var query = _orderRepository.AsQueryable().Where(q => q.State != OrderState.NotRegistered);


            if (update.ListType == OrderListType.Received)
            {
                query = query.Where(o => o.OwnerWallet.ToLower() != null && o.OwnerWallet.ToLower() == walletAddress.ToLower());
            }
            else
            {
                query = query.Where(o => o.SeenBy.Contains(walletAddress.ToLower()));
            }


            if (update.State == OrderState.Completed)
            {
                query = query.Where(o => o.State == OrderState.Completed);
            }
            else
            {
                query = query.Where(o => o.State == OrderState.Pending);
            }


            var totalCount = await query.CountAsync();
            var pagination = update.Pagination;

            var pageCount = (int)Math.Ceiling(
                totalCount / (double)pagination.Size
            );

            var orders = await query
                .OrderByDescending(o => o.CreatedMoment)
                .Skip((pagination.Page - 1) * pagination.Size)
                .Take(pagination.Size)
                .ToListAsync();

            var result = new OrderListResult
            {
                TotalCount = totalCount,
                PageCount = pageCount,
                Data = orders.Select(o => new OrderResult
                {
                    CreatedMoment = o.CreatedMoment,
                    ModifiedMoment = o.ModifiedMoment,
                    OrderId = o.OrderId,
                    TransferId = o.TransferId,

                    OwnerWallet = o.OwnerWallet,
                    PayerWallet = o.PayerWallet,
                    SeenBy = o.SeenBy,

                    TotalAmount = o.TotalAmount,
                    Transportation = o.Transportation,
                    Type = o.Type,
                    State = o.State,
                    PaymentDay = o.PaymentDay
                }).ToList()
            };

            return result;
        }

        /// <summary>
        /// use for get order detail
        /// </summary>
        /// <param name="update"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        /// <exception cref="BadRequestException"></exception>
        public async Task<OrderFullResult> GetOrderDetailAsync(OrderIdUpdate update, string walletAddress)
        {
            var order = await _orderRepository.AsQueryable()
                .Where(q => q.State != OrderState.NotRegistered)
                 .Where(o => (o.OrderId.ToLower() == update.OrderOrTransferId.ToLower()
                           || o.TransferId.ToLower() == update.OrderOrTransferId.ToLower())

                           && (o.OwnerWallet.ToLower() == walletAddress.ToLower() || o.SeenBy.Contains(walletAddress.ToLower())))
                .FirstOrDefaultAsync() ?? throw new NotFoundException("Order not found!");

            if (order == null)
                throw new BadRequestException("Order not found.");

            var query = _invoiceRepository.AsQueryable()
                .Where(q => q.State != InvoiceState.NotRegistered)
                .Where(i => i.OrderId.ToLower() == order.OrderId.ToLower());


            if (order.State == OrderState.Completed)
            {
                query = query.Where(q => q.OwnerWallet.ToLower() == walletAddress.ToLower()
                || q.PayerWallet.ToLower() == walletAddress.ToLower());
            }

            var invoices = await query.ToListAsync();

            var type = order.OwnerWallet.ToLower() == walletAddress.ToLower()
                ? OwnershipType.Owner
                : OwnershipType.Payer;

            var invoiceResults = invoices
                .Select(i => ConvertToReslut(i, type))
                .ToList();

            return ConvertToReslut(invoiceResults, order, type);
        }


        /// <summary>
        /// use for get invoice detail
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task<InvoiceResult> GetInvoiceDetailAsync(InvoiceIdUpdate update, string walletAddress)
        {
            var invoice = await _invoiceRepository.AsQueryable()
                .Where(q => q.State != InvoiceState.NotRegistered)
                .Where(i => i.InvoiceId.ToLower() == update.InvoiceId.ToLower())
                .FirstOrDefaultAsync() ?? throw new NotFoundException("Invoice not found!");

            var type = invoice.OwnerWallet.ToLower() == walletAddress.ToLower()
               ? OwnershipType.Owner
               : OwnershipType.Payer;

            return ConvertToReslut(invoice, type);
        }

        public async Task<LockedInvoiceDetailResult> GetLockedInvoiceDetailAsync(InvoiceIdUpdate update, string walletAddress)
        {
            var invoice = await _invoiceRepository.AsQueryable()
                .Where(i => i.InvoiceId.ToLower() == update.InvoiceId.ToLower() && i.Lock != null)
                .FirstOrDefaultAsync() ?? throw new NotFoundException(ApiResultStatusCode.NotFound, "Locked invoice not found!");

            var chainInvoice = await _blockChainService.GetLockedInvoiceAsync(invoice.InvoiceId, invoice.TokenNetwork);
            var tokenData = ValidateToken(invoice.TokenSymbol);
            var principalAmount = _blockChainService.ConvertFromWei(chainInvoice.PayAmount, tokenData.PriceDecimalPlaces);
            var principalAmountWei = chainInvoice.PayAmount.ToString();
            var livePayoutPreview = _blockChainService.ConvertFromWei(chainInvoice.StakedPayout, tokenData.PriceDecimalPlaces);
            var livePayoutPreviewWei = chainInvoice.StakedPayout.ToString();
            var profitClaimed = _blockChainService.ConvertFromWei(chainInvoice.ProfitClaimed, tokenData.PriceDecimalPlaces);
            var profitClaimedWei = chainInvoice.ProfitClaimed.ToString();
            var lockedUntilMoment = ConvertUnixSecondsToDateTime(chainInvoice.LockedUntil);
            var payerWallet = string.IsNullOrWhiteSpace(invoice.PayerWallet)
                ? NormalizeZeroAddress(chainInvoice.Payer)
                : invoice.PayerWallet;

            EnsureLockedInvoiceDetailAccess(invoice, walletAddress, payerWallet);

            var approverWallet = NormalizeZeroAddress(chainInvoice.Approver);
            var snapshotChangedFilter = Builders<Invoice>.Filter.Or(
                Builders<Invoice>.Filter.Ne(x => x.Lock.LivePayoutPreview, livePayoutPreview),
                Builders<Invoice>.Filter.Ne(x => x.Lock.LivePayoutPreviewWei, livePayoutPreviewWei),
                Builders<Invoice>.Filter.Ne(x => x.Lock.ProfitClaimed, profitClaimed),
                Builders<Invoice>.Filter.Ne(x => x.Lock.ProfitClaimedWei, profitClaimedWei),
                Builders<Invoice>.Filter.Ne(x => x.Lock.Approved, chainInvoice.Approved),
                Builders<Invoice>.Filter.Ne(x => x.Lock.Settled, chainInvoice.Settled),
                Builders<Invoice>.Filter.Ne(x => x.Lock.LockedUntilMoment, lockedUntilMoment),
                Builders<Invoice>.Filter.Ne(x => x.Lock.ApproverWallet, approverWallet));

            var snapshotFilter = Builders<Invoice>.Filter.And(
                Builders<Invoice>.Filter.Eq(x => x.Id, invoice.Id),
                Builders<Invoice>.Filter.Ne(x => x.Lock, null),
                snapshotChangedFilter);

            var snapshotUpdate = Builders<Invoice>.Update
                .Set(x => x.Lock.LivePayoutPreview, livePayoutPreview)
                .Set(x => x.Lock.LivePayoutPreviewWei, livePayoutPreviewWei)
                .Set(x => x.Lock.ProfitClaimed, profitClaimed)
                .Set(x => x.Lock.ProfitClaimedWei, profitClaimedWei)
                .Set(x => x.Lock.Approved, chainInvoice.Approved)
                .Set(x => x.Lock.Settled, chainInvoice.Settled)
                .Set(x => x.Lock.LockedUntilMoment, lockedUntilMoment)
                .Set(x => x.Lock.ApproverWallet, approverWallet);

            var updatedInvoice = await _invoiceRepository.FindOneAndUpdateWithOptionAsync(
                snapshotFilter,
                snapshotUpdate,
                new FindOneAndUpdateOptions<Invoice> { ReturnDocument = ReturnDocument.After });

            if (updatedInvoice != null)
                invoice = updatedInvoice;

            invoice.PayerWallet = payerWallet;

            var isCallerAuthorizedApprover =
                AddressEquals(walletAddress, payerWallet) ||
                (!string.IsNullOrWhiteSpace(invoice.Lock.ApproverWallet) && AddressEquals(walletAddress, invoice.Lock.ApproverWallet));

            var type = invoice.OwnerWallet.ToLower() == walletAddress.ToLower()
               ? OwnershipType.Owner
               : OwnershipType.Payer;

            return ConvertToLockedInvoiceDetailResult(
                invoice,
                type,
                principalAmount,
                principalAmountWei,
                isCallerAuthorizedApprover);
        }


        /// <summary>
        /// use for seen wallet
        /// </summary>
        /// <param name="update"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task<bool> SeenWalletAsync(InvoiceIdUpdate update, string walletAddress)
        {
            var invoice = await _invoiceRepository.AsQueryable()
                .Where(q => q.State != InvoiceState.NotRegistered)
                .Where(i => i.InvoiceId.ToLower() == update.InvoiceId.ToLower())
                .FirstOrDefaultAsync() ?? throw new NotFoundException("Invoice not found!");

            if (invoice.OwnerWallet.ToLower() == walletAddress.ToLower())
                throw new BadRequestException("You are Owner of this invoice!");

            var order = await _orderRepository.AsQueryable()
                .Where(o => o.State != OrderState.NotRegistered)
                .Where(o => o.OrderId.ToLower() == invoice.OrderId.ToLower())
                .FirstOrDefaultAsync() ?? throw new NotFoundException("Order not found!");

            if (order.OwnerWallet.ToLower() == walletAddress.ToLower())
                throw new BadRequestException("You are Owner of this Order!");

            if (order.SeenBy.Contains(walletAddress.ToLower()))
                return true;

            order.SeenBy.Add(walletAddress.ToLower());
            await _orderRepository.ReplaceOneAsync(order);
            return true;
        }


        public async Task<OrderTotalReportResult> GetTotalReportAsync(string walletAddress)
        {
            var ownerData = await GetOwnerOrderReportAsync(walletAddress);
            var paterData = await GetPayerOrderReportAsync(walletAddress);

            return new OrderTotalReportResult
            {
                OwnerReport = ownerData,
                PayerReport = paterData
            };
        }


        /// <summary>
        /// use for get report
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private async Task<OrderReportResult> GetOwnerOrderReportAsync(string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
                throw new ArgumentException("Wallet address is invalid.");

            var orders = await _orderRepository.AsQueryable()
                .Where(o => o.State != OrderState.NotRegistered)
                .Where(o => o.OwnerWallet.ToLower() == walletAddress.ToLower())
                .ToListAsync();

            var pendingOrders = orders.Count(o => o.State == OrderState.Pending);
            var doneOrders = orders.Count(o => o.State == OrderState.Completed);
            var totalOrders = orders.Count();

            var orderProgress = totalOrders == 0
                ? 0
                : Math.Round((decimal)doneOrders / totalOrders * 100, 2);

            //var invoices = await _invoiceRepository.AsQueryable()
            //    .Where(i => i.OwnerWallet.ToLower() == walletAddress.ToLower())
            //    .ToListAsync();

            //var totalInvoices = invoices.Count;
            //var paidInvoices = invoices.Count(i => i.State == InvoiceState.Completed);
            //var pendingInvoices = invoices.Count(i => i.State == InvoiceState.Pending);

            //var invoiceProgress = totalInvoices == 0
            //    ? 0
            //    : Math.Round((decimal)paidInvoices / totalInvoices * 100, 2);

            return new OrderReportResult
            {
                PendingOrderCount = pendingOrders,
                DoneOrderCount = doneOrders,
                OrderProgress = orderProgress,

                //TotalInvoiceCount = totalInvoices,
                //InvoiceCount = totalInvoices,
                //PaidInvoiceCount = paidInvoices,
                //PendingInvoiceCount = pendingInvoices,
                //InvoiceProgress = invoiceProgress
            };
        }

        private async Task<OrderReportResult> GetPayerOrderReportAsync(string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
                throw new ArgumentException("Wallet address is invalid.");

            var orders = await _orderRepository.AsQueryable()
                .Where(o => o.State != OrderState.NotRegistered)
                .Where(o => o.SeenBy.Contains(walletAddress.ToLower()))
                .ToListAsync();

            //var orderids = orders.Select(o => o.OrderId);

            var pendingOrders = orders.Count(o => o.State == OrderState.Pending);
            var doneOrders = orders.Count(o => o.State == OrderState.Completed);
            var totalOrders = orders.Count();

            var orderProgress = totalOrders == 0
                ? 0
                : Math.Round((decimal)doneOrders / totalOrders * 100, 2);

            //var invoices = await _invoiceRepository.AsQueryable()
            //    .Where(i => orderids.Contains(i.OrderId))
            //    .ToListAsync();

            //var totalInvoices = invoices.Count;
            //var paidInvoices = invoices.Count(i => i.State == InvoiceState.Completed && i.PayerWallet.ToLower() == walletAddress.ToLower());
            //var pendingInvoices = invoices.Count(i => i.State == InvoiceState.Pending);

            //var invoiceProgress = totalInvoices == 0
            //    ? 0
            //    : Math.Round((decimal)paidInvoices / totalInvoices * 100, 2);

            return new OrderReportResult
            {
                PendingOrderCount = pendingOrders,
                DoneOrderCount = doneOrders,
                OrderProgress = orderProgress,

                //TotalInvoiceCount = totalInvoices,
                //InvoiceCount = totalInvoices,
                //PaidInvoiceCount = paidInvoices,
                //PendingInvoiceCount = pendingInvoices,
                //InvoiceProgress = invoiceProgress
            };
        }




        /// <summary>
        /// use for remove peding order with invoices
        /// </summary>
        /// <param name="update"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="NotFoundException"></exception>
        /// <exception cref="BadRequestException"></exception>
        public async Task<string> DeletePendingOrderAsync(
        DeletePendingOrderUpdate update,
        string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
                throw new ArgumentException("Wallet address is invalid.");

            var order = await _orderRepository.AsQueryable()
                .Where(o => o.State != OrderState.NotRegistered)
                .Where(o =>
                    o.OwnerWallet.ToLower() == walletAddress.ToLower() &&
                    o.OrderId == update.OrderId)
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException("Order Not Found!");

            if (order.State != OrderState.Pending)
                throw new BadRequestException("Can not remove completed order");

            var invoices = await _invoiceRepository.AsQueryable()
                .Where(i =>
                    i.State != InvoiceState.NotRegistered &&
                    i.OwnerWallet.ToLower() == walletAddress.ToLower() &&
                    i.OrderId == update.OrderId)
                .ToListAsync();


            if (invoices.Count == 0)
                throw new BadRequestException("There is no invoice in order");

            if (invoices.Any(i => i.State != InvoiceState.Pending))
                throw new BadRequestException("There is paid invoice in order");

            var invoiceIds = invoices.Select(q => q.InvoiceId).ToList();

            string txHash = null;

            var tokenNetwork = invoices.Select(q => q.TokenNetwork).FirstOrDefault();
            if (tokenNetwork == "ERC20")
            {
                txHash = await _blockChainService.DeleteERC20MultipleInvoicesAsync(invoiceIds);
            }
            else
            {
                txHash = await _blockChainService.DeleteMultipleInvoicesAsync(invoiceIds);
            }

            if (string.IsNullOrEmpty(txHash))
                throw new BadRequestException("Blockchain transaction failed");

            var filterdb = Builders<Invoice>.Filter.In(
            i => i.InvoiceId,
            invoiceIds);

            var updatedb = Builders<Invoice>.Update
                .Set(i => i.IsDeleted, true)
                .Set(i => i.RemoveHash, txHash)
                .Set(i => i.DeletedMoment, DateTime.UtcNow);

            await _invoiceRepository.UpdateManyAsync(filterdb, updatedb);
            await _orderRepository.DeleteByIdAsync(order.Id);

            _logger.LogInformation(
                "Pending order deleted successfully. OrderId: {OrderId}, TxHash: {TxHash}",
                update.OrderId,
                txHash);

            return order.OrderId;
        }

        private async Task<Invoice> GetInvoiceForLockedSyncAsync(string invoiceId, string hash, BlockchainEventType eventType)
        {
            var invoice = await _invoiceRepository.AsQueryable()
                .Where(q => q.InvoiceId.ToLower() == invoiceId.ToLower())
                .FirstOrDefaultAsync();

            if (invoice == null)
            {
                _logger.LogError(
                    "{EventType} received for missing invoice draft. InvoiceId: {InvoiceId}, Hash: {Hash}",
                    eventType,
                    invoiceId,
                    hash);
                SentrySdk.CaptureMessage($"{eventType} missing invoice draft for InvoiceId {invoiceId}");
                return null;
            }

            if (invoice.Lock == null)
            {
                LogInvoiceModeMismatch(invoiceId, hash, eventType, "locked event for normal draft");
                return null;
            }

            return invoice;
        }

        private async Task<bool> PromoteLockedInvoiceCoarseAsync(Invoice invoice)
        {
            var filter = Builders<Invoice>.Filter.And(
                Builders<Invoice>.Filter.Eq(x => x.Id, invoice.Id),
                Builders<Invoice>.Filter.Ne(x => x.Lock, null),
                Builders<Invoice>.Filter.In(
                    x => x.State,
                    new[] { InvoiceState.NotRegistered, InvoiceState.Pending })
            );

            var update = Builders<Invoice>.Update
                .Set(x => x.State, InvoiceState.Locked);

            var updated = await _invoiceRepository.FindOneAndUpdateWithOptionAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Invoice> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
                return false;

            await PromoteOrderToPendingAsync(invoice.OrderId);
            return true;
        }

        private async Task<bool> PromoteOrderToPendingAsync(string orderId)
        {
            var filter = Builders<Order>.Filter.And(
                Builders<Order>.Filter.Eq(o => o.OrderId, orderId),
                Builders<Order>.Filter.Eq(o => o.State, OrderState.NotRegistered)
            );

            var update = Builders<Order>.Update
                .Set(o => o.State, OrderState.Pending);

            var updated = await _orderRepository.FindOneAndUpdateWithOptionAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Order> { ReturnDocument = ReturnDocument.After });

            return updated != null;
        }

        private async Task<bool> UpdateLockedInvoiceIfNeededAsync(
            Invoice invoice,
            FilterDefinition<Invoice> changedFilter,
            UpdateDefinition<Invoice> update,
            FilterDefinition<Invoice> extraFilter = null)
        {
            var filter = Builders<Invoice>.Filter.And(
                Builders<Invoice>.Filter.Eq(x => x.Id, invoice.Id),
                Builders<Invoice>.Filter.Ne(x => x.Lock, null),
                changedFilter
            );

            if (extraFilter != null)
            {
                filter = Builders<Invoice>.Filter.And(filter, extraFilter);
            }

            var updated = await _invoiceRepository.FindOneAndUpdateWithOptionAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Invoice> { ReturnDocument = ReturnDocument.After });

            return updated != null;
        }

        private static LockDetail CreateLockDetail(bool isLocked, int lockDurationMonths, string approverWallet)
        {
            if (!isLocked)
                return null;

            return new LockDetail
            {
                DurationMonths = lockDurationMonths,
                ApproverWallet = approverWallet,
                State = LockState.Created
            };
        }

        private static int ValidateLockDurationMonths(int? lockDurationMonths)
        {
            if (!lockDurationMonths.HasValue)
                throw new BadRequestException("Lock duration is required for locked invoices.");

            if (!AllowedLockDurations.Contains(lockDurationMonths.Value))
                throw new BadRequestException("Lock duration must be one of 1, 3, 6, 12, 18, or 24 months.");

            return lockDurationMonths.Value;
        }

        private string ValidateThirdPartyApprover(string thirdPartyApprover, string ownerWallet)
        {
            if (string.IsNullOrWhiteSpace(thirdPartyApprover))
                return null;

            var approverWallet = thirdPartyApprover.Trim();
            var addressUtil = AddressUtil.Current;

            if (!addressUtil.IsValidAddressLength(approverWallet) ||
                !addressUtil.IsValidEthereumAddressHexFormat(approverWallet))
                throw new BadRequestException("Third party approver must be a valid EVM address.");

            if (!addressUtil.IsChecksumAddress(approverWallet))
                approverWallet = addressUtil.ConvertToChecksumAddress(approverWallet);

            if (AddressEquals(approverWallet, ownerWallet))
                throw new BadRequestException("Third party approver cannot be the invoice owner.");

            if (AddressEquals(approverWallet, ZeroAddress))
                throw new BadRequestException("Third party approver cannot be the zero address.");

            return approverWallet;
        }

        private static string ValidateNormalInvoiceHasNoLockFields(int? lockDurationMonths, string thirdPartyApprover)
        {
            if (lockDurationMonths.HasValue || !string.IsNullOrWhiteSpace(thirdPartyApprover))
                throw new BadRequestException("Lock fields are only valid for locked invoices.");

            return null;
        }

        private static string ValidateNormalMultiStepOrderHasNoLockFields(CreateMultiStepOrderUpdate update)
        {
            if (!string.IsNullOrWhiteSpace(update.ThirdPartyApprover))
                throw new BadRequestException("Third party approver is only valid for locked orders.");

            if (update.Invoices.Any(i => i.LockDurationMonths.HasValue))
                throw new BadRequestException("Lock duration is only valid for locked orders.");

            return null;
        }

        private decimal ConvertInvoiceTokenAmount(Invoice invoice, BigInteger amount)
        {
            var tokenData = ValidateToken(invoice.TokenSymbol);
            return _blockChainService.ConvertFromWei(amount, tokenData.PriceDecimalPlaces);
        }

        private LockedInvoiceSyncResult BuildLockedSyncResult(
            Invoice invoice,
            bool changed,
            string payerWallet,
            string approverWallet,
            string beneficiaryWallet,
            LockState lockState,
            InvoiceState invoiceState)
        {
            return new LockedInvoiceSyncResult
            {
                Changed = changed,
                InvoiceId = invoice.InvoiceId,
                OrderId = invoice.OrderId,
                OwnerWallet = invoice.OwnerWallet,
                PayerWallet = string.IsNullOrWhiteSpace(payerWallet) ? invoice.PayerWallet : payerWallet,
                ApproverWallet = string.IsNullOrWhiteSpace(approverWallet) ? invoice.Lock.ApproverWallet : approverWallet,
                BeneficiaryWallet = beneficiaryWallet,
                LockState = lockState,
                InvoiceState = invoiceState
            };
        }

        private void LogInvoiceModeMismatch(string invoiceId, string hash, BlockchainEventType eventType, string reason)
        {
            _logger.LogError(
                "Invoice mode mismatch. Reason: {Reason}, EventType: {EventType}, InvoiceId: {InvoiceId}, Hash: {Hash}",
                reason,
                eventType,
                invoiceId,
                hash);
            SentrySdk.CaptureMessage($"Invoice mode mismatch: {reason}, EventType {eventType}, InvoiceId {invoiceId}");
        }

        private static string NormalizeZeroAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            return AddressEquals(address, ZeroAddress) ? null : address;
        }

        private static bool AddressEquals(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool OptionalAddressEquals(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
                return true;

            return AddressEquals(left, right);
        }

        private static bool IsTerminalOrderInvoice(Invoice invoice)
        {
            return invoice.State == InvoiceState.Completed;
        }

        private static void EnsureLockedInvoiceDetailAccess(Invoice invoice, string walletAddress, string payerWallet)
        {
            if (AddressEquals(walletAddress, invoice.OwnerWallet) ||
                AddressEquals(walletAddress, payerWallet) ||
                AddressEquals(walletAddress, invoice.Lock.ApproverWallet))
            {
                return;
            }

            throw new BaseException(ApiResultStatusCode.Forbidden, "Access denied", System.Net.HttpStatusCode.Forbidden);
        }

        private void LogLockedInvoiceDraftMismatch(string invoiceId, string hash, string fieldName, string expected, string actual)
        {
            _logger.LogError(
                "LockedInvoiceCreated draft mismatch. Field: {FieldName}, InvoiceId: {InvoiceId}, Expected: {Expected}, Actual: {Actual}, Hash: {Hash}",
                fieldName,
                invoiceId,
                expected,
                actual,
                hash);
            SentrySdk.CaptureMessage($"LockedInvoiceCreated draft mismatch: {fieldName}, InvoiceId {invoiceId}");
        }

        private static DateTime? ConvertUnixSecondsToDateTime(BigInteger unixSeconds)
        {
            if (unixSeconds <= BigInteger.Zero)
                return null;

            return DateTimeOffset.FromUnixTimeSeconds(checked((long)unixSeconds)).UtcDateTime;
        }





        /// <summary>
        /// use for convert to result
        /// </summary>
        /// <param name="invoice"></param>
        /// <returns></returns>
        private InvoiceResult ConvertToReslut(Invoice invoice, OwnershipType type)
        {
            return new InvoiceResult
            {
                CreatedMoment = invoice.CreatedMoment,
                ModifiedMoment = invoice.ModifiedMoment,
                InvoiceId = invoice.InvoiceId,
                OwnerWallet = invoice.OwnerWallet,
                PayerWallet = invoice.PayerWallet,
                OrderId = invoice.OrderId,
                TokenSymbol = invoice.TokenSymbol,
                TokenNetwork = invoice.TokenNetwork,
                TokenAddress = invoice.TokenAddress,
                USDTAmount = invoice.USDTAmount,
                USDTAmountInWei = invoice.USDTAmountInWei,
                Desctiption = invoice.Desctiption,
                TokenAmountAtPayment = invoice.TokenAmountAtPayment,
                TokenAmountWeiAtPayment = invoice.TokenAmountWeiAtPayment,
                TokenPriceAtPayment = invoice.TokenPriceAtPayment,
                State = invoice.State,
                PayMoment = invoice.PayMoment,
                RegisterHash = invoice.RegisterHash,
                PaymentHash = invoice.PaymentHash,
                ActivateDate = invoice.ActivateDate,
                IsLocked = invoice.Lock != null,
                LockDurationMonths = invoice.Lock?.DurationMonths,
                ApproverWallet = invoice.Lock?.ApproverWallet,
                OwnershipType = type
            };

        }

        private LockedInvoiceDetailResult ConvertToLockedInvoiceDetailResult(
            Invoice invoice,
            OwnershipType type,
            decimal principalAmount,
            string principalAmountWei,
            bool isCallerAuthorizedApprover)
        {
            return new LockedInvoiceDetailResult
            {
                CreatedMoment = invoice.CreatedMoment,
                ModifiedMoment = invoice.ModifiedMoment,
                InvoiceId = invoice.InvoiceId,
                OwnerWallet = invoice.OwnerWallet,
                PayerWallet = invoice.PayerWallet,
                OrderId = invoice.OrderId,
                TokenSymbol = invoice.TokenSymbol,
                TokenNetwork = invoice.TokenNetwork,
                TokenAddress = invoice.TokenAddress,
                USDTAmount = invoice.USDTAmount,
                USDTAmountInWei = invoice.USDTAmountInWei,
                Desctiption = invoice.Desctiption,
                TokenAmountAtPayment = invoice.TokenAmountAtPayment,
                TokenAmountWeiAtPayment = invoice.TokenAmountWeiAtPayment,
                TokenPriceAtPayment = invoice.TokenPriceAtPayment,
                State = invoice.State,
                PayMoment = invoice.PayMoment,
                RegisterHash = invoice.RegisterHash,
                PaymentHash = invoice.PaymentHash,
                ActivateDate = invoice.ActivateDate,
                IsLocked = true,
                LockDurationMonths = invoice.Lock.DurationMonths,
                ApproverWallet = invoice.Lock.ApproverWallet,
                OwnershipType = type,
                LockState = invoice.Lock.State,
                LockedUntilMoment = invoice.Lock.LockedUntilMoment,
                ApprovedMoment = invoice.Lock.ApprovedMoment,
                ApprovedBy = invoice.Lock.ApprovedBy,
                ApproveHash = invoice.Lock.ApproveHash,
                BeneficiaryWallet = invoice.Lock.BeneficiaryWallet,
                ResolveHash = invoice.Lock.ResolveHash,
                StakedPayout = invoice.Lock.StakedPayout,
                StakedPayoutWei = invoice.Lock.StakedPayoutWei,
                FeeAmount = invoice.Lock.FeeAmount,
                FeeAmountWei = invoice.Lock.FeeAmountWei,
                PrincipalAmount = principalAmount,
                PrincipalAmountWei = principalAmountWei,
                LivePayoutPreview = invoice.Lock.LivePayoutPreview,
                LivePayoutPreviewWei = invoice.Lock.LivePayoutPreviewWei,
                ProfitClaimed = invoice.Lock.ProfitClaimed,
                ProfitClaimedWei = invoice.Lock.ProfitClaimedWei,
                Approved = invoice.Lock.Approved,
                Settled = invoice.Lock.Settled,
                IsCallerAuthorizedApprover = isCallerAuthorizedApprover
            };
        }

        ///// <summary>
        ///// use for convert to result
        ///// </summary>
        ///// <param name="invoice"></param>
        ///// <returns></returns>
        //private InvoiceResult ConvertToReslut(Invoice invoice,string walletAddress)
        //{
        //    return new InvoiceResult
        //    {
        //        CreatedMoment = invoice.CreatedMoment,
        //        ModifiedMoment = invoice.ModifiedMoment,
        //        InvoiceId = invoice.InvoiceId,
        //        OwnerWallet = invoice.OwnerWallet,
        //        PayerWallet = invoice.PayerWallet,
        //        OrderId = invoice.OrderId,
        //        TokenSymbol = invoice.TokenSymbol,
        //        TokenAddress = invoice.TokenAddress,
        //        USDTAmount = invoice.USDTAmount,
        //        USDTAmountInWei = invoice.USDTAmountInWei,
        //        Desctiption = invoice.Desctiption,
        //        TokenAmountAtPayment = invoice.TokenAmountAtPayment,
        //        TokenAmountWeiAtPayment = invoice.TokenAmountWeiAtPayment,
        //        TokenPriceAtPayment = invoice.TokenPriceAtPayment,
        //        State = invoice.State,
        //        PayMoment = invoice.PayMoment,
        //        RegisterHash = invoice.RegisterHash,
        //        PaymentHash = invoice.PaymentHash,
        //        ActivateDate = invoice.ActivateDate,
        //        OwnershipType = invoice.OwnerWallet.ToLower() == walletAddress.ToLower()
        //            ? OwnershipType.Owner
        //            : OwnershipType.Payer
        //    };

        //}



        /// <summary>
        /// use for convert to result
        /// </summary>
        /// <param name="invoiceResults"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        private OrderFullResult ConvertToReslut(List<InvoiceResult> invoiceResults, Order order, OwnershipType type)
        {
            return new OrderFullResult
            {
                CreatedMoment = order.CreatedMoment,
                ModifiedMoment = order.ModifiedMoment,
                OrderId = order.OrderId,
                OwnerWallet = order.OwnerWallet,
                PayerWallet = order.PayerWallet,
                SeenBy = order.SeenBy,
                TotalAmount = order.TotalAmount,
                Transportation = order.Transportation,
                Type = order.Type,
                State = order.State,
                PaymentDay = order.PaymentDay,
                TransferId = order.TransferId,
                Invoices = invoiceResults,
                OwnershipType = type
            };
        }



        /// <summary>
        /// use for validate token
        /// </summary>
        /// <param name="tokenName"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        private AvailableTokenData ValidateToken(string tokenName)
        {
            var tokenData = _availableTokenSetting.FirstOrDefault(q => q.Name.ToLower() == tokenName.ToLower())
                ?? throw new BadRequestException($"Unsupported token name! {tokenName}");
            return tokenData;
        }


        public static string GenerateBytes32HexId()
        {
            var buffer = new byte[32];
            RandomNumberGenerator.Fill(buffer);

            var newId = BitConverter.ToString(buffer)
                .Replace("-", "")
                .ToLowerInvariant();

            return newId;
        }


    }
}

///// <summary>
///// use for create quick order
///// </summary>
///// <param name="update"></param>
///// <param name="walletAddress"></param>
///// <returns></returns>
//public async Task<OrderFullResult> CreateQuickOrderAsync(CreateQuickInvoiceUpdate update, string walletAddress)
//{
//    var newOrder = new Order
//    {
//        OrderId = Guid.NewGuid().ToString("N"),
//        OwnerWallet = walletAddress,
//        PayerWallet = null,
//        SeenBy = [],
//        State = OrderState.Pending,
//        PaymentDay = null,
//        Type = OrderType.Quick,
//        Transportation = null,
//        TotalAmount = update.Amount,
//        TransferId = _randomService.GetSecureAlphaNumericString(12).ToUpper(),

//    };


//    await _orderRepository.InsertOneAsync(newOrder);
//    try
//    {
//        var invoiceResult = await CreateQuickInvoiceAsync(newOrder, update.TokenSymbol, update.Description);
//        return ConvertToReslut(new List<InvoiceResult> { invoiceResult }, newOrder, OwnershipType.Owner);
//    }
//    catch (Exception ex)
//    {
//        SentrySdk.CaptureMessage($"Error creating quick invoice for order ex : {ex.Message}");
//        await _orderRepository.DeleteByIdAsync(newOrder.Id);
//        throw new BadRequestException("Please try later!");
//    }
//}


///// <summary>
///// use for create multi step order
///// </summary>
///// <param name="update"></param>
///// <param name="walletAddress"></param>
///// <returns></returns>
///// <exception cref="BadRequestException"></exception>
//public async Task<OrderFullResult> CreateMultiStepOrderAsync(
//  CreateMultiStepOrderUpdate update,
//  string walletAddress)
//{
//    if (update == null)
//        throw new BadRequestException(nameof(update));

//    if (update.Invoices == null || !update.Invoices.Any())
//        throw new BadRequestException("Invoices list cannot be empty.");

//    var invoicesTotal = update.Invoices.Sum(i => i.Amount);
//    if (invoicesTotal != update.TotalAmount)
//        throw new BadRequestException(
//            "Sum of invoice amounts does not match order total amount.");

//    var newOrder = new Order
//    {
//        OrderId = Guid.NewGuid().ToString("N"),
//        OwnerWallet = walletAddress,
//        PayerWallet = null,
//        SeenBy = [],
//        State = OrderState.Pending,
//        PaymentDay = null,
//        Type = OrderType.Multi,
//        Transportation = update.Transportation.Trim(),
//        TotalAmount = update.TotalAmount,
//        TransferId = _randomService.GetSecureAlphaNumericString(12).ToUpper(),
//    };

//    await _orderRepository.InsertOneAsync(newOrder);
//    try
//    {
//        var invoiceResults = await CreateMultiStepInvoicesAsync(newOrder, update.Invoices, walletAddress);

//        return ConvertToReslut(invoiceResults, newOrder, OwnershipType.Owner);
//    }
//    catch (Exception ex)
//    {
//        SentrySdk.CaptureMessage($"Error creating multi-step invoices for order ex : {ex.Message}");
//        await _orderRepository.DeleteByIdAsync(newOrder.Id);
//        throw new BadRequestException("Please try later!");
//    }

//}

///// <summary>
///// use for create quick invoice
///// </summary>
///// <param name="order"></param>
///// <param name="token"></param>
///// <param name="desc"></param>
///// <param name="dateOnly"></param>
///// <returns></returns>
//private async Task<InvoiceResult> CreateQuickInvoiceAsync(Order order, string token, string desc, DateOnly? dateOnly = null)
//{

//    var tokenData = ValidateToken(token);

//    var activeDate = dateOnly.HasValue
//        ? dateOnly.Value.ToDateTime(TimeOnly.MinValue)
//        : DateOnly.FromDateTime(DateTime.Now)
//            .ToDateTime(TimeOnly.MinValue);

//    var newInvoice = new Invoice
//    {
//        InvoiceId = GenerateBytes32HexId(),
//        TokenSymbol = tokenData.Name,
//        TokenAddress = tokenData.Address,
//        USDTAmount = order.TotalAmount,
//        USDTAmountInWei = _blockChainService.ConvertToWei(order.TotalAmount, 18).ToString(),
//        OwnerWallet = order.OwnerWallet,
//        OrderId = order.OrderId,
//        PayerWallet = null,
//        State = InvoiceState.Pending,
//        ActivateDate = activeDate,
//        PayMoment = null,
//        Desctiption = desc.Trim(),
//        RegisterHash = null,
//        PaymentHash = null,
//        Errors = null,
//        TokenAmountAtPayment = null,
//        TokenAmountWeiAtPayment = null,
//        TokenPriceAtPayment = null,
//    };

//    var registerHash = await _blockChainService.CreateQuickInvoiceAsync(newInvoice.InvoiceId, newInvoice.TokenAddress, newInvoice.USDTAmount, newInvoice.OwnerWallet);
//    if (registerHash == null) throw new BadRequestException("There is a problem, try later!");

//    newInvoice.RegisterHash = registerHash;
//    await _invoiceRepository.InsertOneAsync(newInvoice);
//    return ConvertToReslut(newInvoice, OwnershipType.Owner);
//}


///// <summary>
///// use for create multi step invoices
///// </summary>
///// <param name="order"></param>
///// <param name="invoiceUpdates"></param>
///// <returns></returns>
///// <exception cref="BadRequestException"></exception>
///// <exception cref="Exception"></exception>
//private async Task<List<InvoiceResult>> CreateMultiStepInvoicesAsync(
//Order order,
//List<MultiStepInvoiceUpdate> invoiceUpdates, string ownerAddress)
//{
//    if (invoiceUpdates == null || !invoiceUpdates.Any())
//        throw new BadRequestException("Invoice list is empty.");

//    var invoices = new List<Invoice>();
//    var blockchainInputs = new List<CreateMultipleInvoicesUpdate>();

//    foreach (var invoiceUpdate in invoiceUpdates)
//    {
//        var tokenData = ValidateToken(invoiceUpdate.TokenSymbol);

//        var nowPlus1 = DateTime.Now.AddMinutes(1);
//        var timeOnly = TimeOnly.FromDateTime(nowPlus1);
//        var activeDate = invoiceUpdate.ActivationDate.ToDateTime(timeOnly);

//        var invoice = new Invoice
//        {
//            InvoiceId = GenerateBytes32HexId(),
//            TokenSymbol = tokenData.Name,
//            TokenAddress = tokenData.Address,
//            USDTAmount = invoiceUpdate.Amount,
//            USDTAmountInWei = _blockChainService
//                .ConvertToWei(invoiceUpdate.Amount, 18)
//                .ToString(),

//            OwnerWallet = order.OwnerWallet,
//            OrderId = order.OrderId,
//            PayerWallet = null,
//            State = InvoiceState.Pending,
//            ActivateDate = activeDate,
//            Desctiption = invoiceUpdate.Description?.Trim(),

//            PayMoment = null,
//            RegisterHash = null,
//            PaymentHash = null,
//            Errors = null,

//            TokenAmountAtPayment = null,
//            TokenAmountWeiAtPayment = null,
//            TokenPriceAtPayment = null,
//        };

//        invoices.Add(invoice);

//        blockchainInputs.Add(new CreateMultipleInvoicesUpdate
//        {
//            Id = invoice.InvoiceId,
//            TokenAddress = invoice.TokenAddress,
//            USDTAmount = invoice.USDTAmount,
//            UnLockTime = activeDate
//        });
//    }

//    var txHash = await _blockChainService
//        .CreateMultipleInvoicesAsync(blockchainInputs, ownerAddress);

//    if (txHash == null) throw new BadRequestException("There is a problem, try later!");

//    if (string.IsNullOrEmpty(txHash))
//        throw new Exception("Blockchain registration failed.");

//    foreach (var invoice in invoices)
//        invoice.RegisterHash = txHash;

//    await _invoiceRepository.InsertManyAsync(invoices);

//    return invoices.Select(invoice => ConvertToReslut(invoice, OwnershipType.Owner)).ToList();
//}
