using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Utilities;
using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using SLT.Services._BlockChain;
using SLT.Services._BlockChain.DTOs.Updates;
using SLT.Services._Order.DTOs.Results;
using SLT.Services._Order.DTOs.Updates;
using SLT.Services._Price.DTOs.Settings;
using System.Security.Cryptography;
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

        /// <summary>
        /// use for create quick order
        /// </summary>
        /// <param name="update"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        public async Task<OrderFullResult> CreateQuickOrderAsync(CreateQuickInvoiceUpdate update, string walletAddress)
        {
            var newOrder = new Order
            {
                OrderId = Guid.NewGuid().ToString("N"),
                OwnerWallet = walletAddress,
                PayerWallet = null,
                SeenBy = [],
                State = OrderState.Pending,
                PaymentDay = null,
                Type = OrderType.Quick,
                Transportation = null,
                TotalAmount = update.Amount,
                TransferId = _randomService.GetSecureAlphaNumericString(12).ToUpper(),

            };


            await _orderRepository.InsertOneAsync(newOrder);
            try
            {
                var invoiceResult = await CreateQuickInvoiceAsync(newOrder, update.TokenSymbol, update.Description);
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
        /// use for create multi step order
        /// </summary>
        /// <param name="update"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        public async Task<OrderFullResult> CreateMultiStepOrderAsync(
          CreateMultiStepOrderUpdate update,
          string walletAddress)
        {
            if (update == null)
                throw new BadRequestException(nameof(update));

            if (update.Invoices == null || !update.Invoices.Any())
                throw new BadRequestException("Invoices list cannot be empty.");

            var invoicesTotal = update.Invoices.Sum(i => i.Amount);
            if (invoicesTotal != update.TotalAmount)
                throw new BadRequestException(
                    "Sum of invoice amounts does not match order total amount.");

            var newOrder = new Order
            {
                OrderId =  Guid.NewGuid().ToString("N"),
                OwnerWallet = walletAddress,
                PayerWallet = null,
                SeenBy = [],
                State = OrderState.Pending,
                PaymentDay = null,
                Type = OrderType.Multi,
                Transportation = update.Transportation.Trim(),
                TotalAmount = update.TotalAmount,
                TransferId = _randomService.GetSecureAlphaNumericString(12).ToUpper(),
            };

            await _orderRepository.InsertOneAsync(newOrder);
            try
            {
                var invoiceResults = await CreateMultiStepInvoicesAsync(newOrder, update.Invoices);

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
        /// use for sync paid invoice
        /// </summary>
        /// <param name="invoiceId"></param>
        /// <param name="payerWallet"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public async Task<string> SyncPaidInvoiceAsync(string invoiceId, string payerWallet, string hash)
        {
            var invoice = await _invoiceRepository.AsQueryable()
                .Where(q => q.InvoiceId.ToLower() == invoiceId.ToLower() && q.State == InvoiceState.Pending)
                .FirstOrDefaultAsync();

            if(invoice == null) return null;

            invoice.PaymentHash = hash;
            invoice.PayMoment = DateTime.UtcNow;
            invoice.PayerWallet = payerWallet;
            invoice.State = InvoiceState.Completed;
            await _invoiceRepository.ReplaceOneAsync(invoice);

            await SyncOrderWithOrderAsync(invoice.OrderId);

            return invoice.OwnerWallet;
        }



        /// <summary>
        /// use for sync transaction log with order
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task SyncOrderWithOrderAsync(string orderId)
        {
            var order = await _orderRepository.AsQueryable()
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
                var hasPaidInvoice = invoices.Any(i => i.State == InvoiceState.Completed);

                if (hasPaidInvoice)
                {
                    order.State = OrderState.Completed;
                    order.PaymentDay = DateTime.UtcNow;
                    await _orderRepository.ReplaceOneAsync(order);
                }

                return;
            }

            if (order.Type == OrderType.Multi)
            {
                var allInvoicesPaid = invoices.All(i => i.State == InvoiceState.Completed);

                if (allInvoicesPaid)
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

            var query = _orderRepository.AsQueryable();


            if (update.ListType == OrderListType.Sent)
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
                 .Where(o => (o.OrderId.ToLower() == update.OrderOrTransferId.ToLower()
                           || o.TransferId.ToLower() == update.OrderOrTransferId.ToLower())

                           && (o.OwnerWallet.ToLower() == walletAddress.ToLower() || o.SeenBy.Contains(walletAddress.ToLower())))
                .FirstOrDefaultAsync() ?? throw new NotFoundException("Order not found!");

            if (order == null)
                throw new BadRequestException("Order not found.");

            var query =  _invoiceRepository.AsQueryable()
                .Where(i => i.OrderId.ToLower() == order.OrderId.ToLower());
                

            if (order.State == OrderState.Completed)
            {
                query = query.Where(q => q.OwnerWallet.ToLower() == walletAddress.ToLower() || q.PayerWallet.ToLower() == walletAddress.ToLower());
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
        public async Task<InvoiceResult> GetInvoiceDetailAsync(InvoiceIdUpdate update,string walletAddress)
        {
            var invoice =  await _invoiceRepository.AsQueryable()
                .Where(i => i.InvoiceId.ToLower() == update.InvoiceId.ToLower())
                .FirstOrDefaultAsync() ?? throw new NotFoundException("Invoice not found!");

            var type = invoice.OwnerWallet.ToLower() == walletAddress.ToLower()
               ? OwnershipType.Owner
               : OwnershipType.Payer;

            return ConvertToReslut(invoice, type);
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
              .Where(i => i.InvoiceId.ToLower() == update.InvoiceId.ToLower())
              .FirstOrDefaultAsync() ?? throw new NotFoundException("Invoice not found!");

            if (invoice.OwnerWallet.ToLower() == walletAddress.ToLower())
                throw new BadRequestException("You are Owner of this invoice!");

            var order = await _orderRepository.AsQueryable()
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



        /// <summary>
        /// use for get report
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<OrderReportResult> GetOrderReportAsync(string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
                throw new ArgumentException("Wallet address is invalid.");

            var orders = await _orderRepository.AsQueryable()
                .Where(o => o.OwnerWallet.ToLower() == walletAddress.ToLower())
                .ToListAsync();

            var pendingOrders = orders.Count(o => o.State == OrderState.Pending);
            var doneOrders = orders.Count(o => o.State == OrderState.Completed);
            var totalOrders = orders.Count();

            var orderProgress = totalOrders == 0
                ? 0
                : Math.Round((decimal)doneOrders / totalOrders * 100, 2);

            var invoices = await _invoiceRepository.AsQueryable()
                .Where(i => i.OwnerWallet.ToLower() == walletAddress.ToLower())
                .ToListAsync();

            var totalInvoices = invoices.Count;
            var paidInvoices = invoices.Count(i => i.State == InvoiceState.Completed);
            var pendingInvoices = invoices.Count(i => i.State == InvoiceState.Pending);

            var invoiceProgress = totalInvoices == 0
                ? 0
                : Math.Round((decimal)paidInvoices / totalInvoices * 100, 2);

            return new OrderReportResult
            {
                TotalInvoiceCount = totalInvoices,
                PendingOrderCount = pendingOrders,
                DoneOrderCount = doneOrders,
                OrderProgress = orderProgress,

                InvoiceCount = totalInvoices,
                PaidInvoiceCount = paidInvoices,
                PendingInvoiceCount = pendingInvoices,
                InvoiceProgress = invoiceProgress
            };
        }



        /// <summary>
        /// use for create quick invoice
        /// </summary>
        /// <param name="order"></param>
        /// <param name="token"></param>
        /// <param name="desc"></param>
        /// <param name="dateOnly"></param>
        /// <returns></returns>
        private async Task<InvoiceResult> CreateQuickInvoiceAsync(Order order, string token, string desc, DateOnly? dateOnly = null)
        {

            var tokenData = ValidateToken(token);

            var activeDate = dateOnly.HasValue
                ? dateOnly.Value.ToDateTime(TimeOnly.MinValue)
                : DateOnly.FromDateTime(DateTime.Now)
                    .ToDateTime(TimeOnly.MinValue);

            var newInvoice = new Invoice
            {
                InvoiceId = GenerateBytes32HexId(),
                TokenSymbol = tokenData.Name,
                TokenAddress = tokenData.Address,
                USDTAmount = order.TotalAmount,
                USDTAmountInWei = _blockChainService.ConvertToWei(order.TotalAmount, 18).ToString(),
                OwnerWallet = order.OwnerWallet,
                OrderId = order.OrderId,
                PayerWallet = null,
                State = InvoiceState.Pending,
                ActivateDate = activeDate,
                PayMoment = null,
                Desctiption = desc.Trim(),
                RegisterHash = null,
                PaymentHash = null,
                Errors = null,
                TokenAmountAtPayment = null,
                TokenAmountWeiAtPayment = null,
                TokenPriceAtPayment = null,
            };

            var registerHash = await _blockChainService.CreateQuickInvoiceAsync(newInvoice.InvoiceId, newInvoice.TokenAddress, newInvoice.USDTAmount);
            if (registerHash == null) throw new BadRequestException("There is a problem, try later!");

            newInvoice.RegisterHash = registerHash;
            await _invoiceRepository.InsertOneAsync(newInvoice);
            return ConvertToReslut(newInvoice,OwnershipType.Owner);
        }


        /// <summary>
        /// use for create multi step invoices
        /// </summary>
        /// <param name="order"></param>
        /// <param name="invoiceUpdates"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        /// <exception cref="Exception"></exception>
        private async Task<List<InvoiceResult>> CreateMultiStepInvoicesAsync(
        Order order,
        List<MultiStepInvoiceUpdate> invoiceUpdates)
        {
            if (invoiceUpdates == null || !invoiceUpdates.Any())
                throw new BadRequestException("Invoice list is empty.");

            var invoices = new List<Invoice>();
            var blockchainInputs = new List<CreateMultipleInvoicesUpdate>();

            foreach (var invoiceUpdate in invoiceUpdates)
            {
                var tokenData = ValidateToken(invoiceUpdate.TokenSymbol);

                //var activeDate = invoiceUpdate.ActivationDate.HasValue ? invoiceUpdate.ActivationDate.Value.ToDateTime(TimeOnly.MinValue) : DateOnly.FromDateTime(DateTime.Now).ToDateTime(TimeOnly.MinValue);
                var activeDate = invoiceUpdate.ActivationDate.ToDateTime(TimeOnly.MinValue);

                var invoice = new Invoice
                {
                    InvoiceId = GenerateBytes32HexId(),
                    TokenSymbol = tokenData.Name,
                    TokenAddress = tokenData.Address,
                    USDTAmount = invoiceUpdate.Amount,
                    USDTAmountInWei = _blockChainService
                        .ConvertToWei(invoiceUpdate.Amount, 18)
                        .ToString(),

                    OwnerWallet = order.OwnerWallet,
                    OrderId = order.OrderId,
                    PayerWallet = null,
                    State = InvoiceState.Pending,
                    ActivateDate = activeDate,
                    Desctiption = invoiceUpdate.Description?.Trim(),

                    PayMoment = null,
                    RegisterHash = null,
                    PaymentHash = null,
                    Errors = null,

                    TokenAmountAtPayment = null,
                    TokenAmountWeiAtPayment = null,
                    TokenPriceAtPayment = null,
                };

                invoices.Add(invoice);

                blockchainInputs.Add(new CreateMultipleInvoicesUpdate
                {
                    Id = invoice.InvoiceId,
                    TokenAddress = invoice.TokenAddress,
                    USDTAmount = invoice.USDTAmount,
                    UnLockTime = activeDate
                });
            }

            var txHash = await _blockChainService
                .CreateMultipleInvoicesAsync(blockchainInputs);

            if (txHash == null) throw new BadRequestException("There is a problem, try later!");

            if (string.IsNullOrEmpty(txHash))
                throw new Exception("Blockchain registration failed.");

            foreach (var invoice in invoices)
                invoice.RegisterHash = txHash;

            await _invoiceRepository.InsertManyAsync(invoices);

            return invoices.Select(invoice => ConvertToReslut(invoice, OwnershipType.Owner)).ToList();
        }




        /// <summary>
        /// use for convert to result
        /// </summary>
        /// <param name="invoice"></param>
        /// <returns></returns>
        private InvoiceResult ConvertToReslut(Invoice invoice,OwnershipType type)
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
                OwnershipType = type
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
        private OrderFullResult ConvertToReslut(List<InvoiceResult> invoiceResults, Order order , OwnershipType type)
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

            var newId =  BitConverter.ToString(buffer)
                .Replace("-", "")
                .ToLowerInvariant();

            return newId;
        }

       
    }
}
