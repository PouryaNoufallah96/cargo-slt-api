using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SLT.Services._Order;
using SLT.Services._Order.DTOs.Results;
using SLT.Services._Order.DTOs.Updates;
using Swashbuckle.AspNetCore.Annotations;
using Utilities.Api;
using Utilities.Filters;

namespace SLT.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class OrderController(IOrderService _orderService) : ApiBaseController
    {

        [HttpPost("[action]")]
        [Authorize(RequireActiveUser = true)]
        [SwaggerOperation(Summary = "Create quick order", Tags = ["Order"])]
        public async Task<OrderFullResult> CreateQuickOrderAsync(
            CreateQuickInvoiceUpdate update)
        {

            return await _orderService.CreateQuickOrderAsync(
                update,
                WalletAddress
            );
        }

        [HttpPost("[action]")]
        [Authorize(RequireActiveUser = true)]
        [SwaggerOperation(Summary = "Create multi step order", Tags = ["Order"])]
        public async Task<OrderFullResult> CreateMultiStepOrderAsync(
            CreateMultiStepOrderUpdate update)
        {
            return await _orderService.CreateMultiStepOrderAsync(
                update,
                WalletAddress
            );
        }

        [HttpPost("[action]")]
        [Authorize(RequireActiveUser = false)]
        [SwaggerOperation(Summary = "Get user orders list", Tags = ["Order"])]
        public async Task<OrderListResult> GetOrderListAsync(
            GetPendingOrderListUpdate update)
        {
            return await _orderService.GetOrderListAsync(
                update,
                WalletAddress
            );
        }

        [HttpPost("[action]")]
        [Authorize(RequireActiveUser = false)]
        [SwaggerOperation(Summary = "Get order detail", Tags = ["Order"])]
        public async Task<OrderFullResult> GetOrderDetailAsync(
            OrderIdUpdate update)
        {
            return await _orderService.GetOrderDetailAsync(
                update,
                WalletAddress
            );
        }


        [HttpPost("[action]")]
        [Authorize(RequireActiveUser = false)]
        [SwaggerOperation(Summary = "Get invoice detail", Tags = ["Invoice"])]
        public async Task<InvoiceResult> GetInvoiceDetailAsync(
            InvoiceIdUpdate update)
        {
            return await _orderService.GetInvoiceDetailAsync(update,WalletAddress);
        }


        [HttpPost("[action]")]
        [Authorize(RequireActiveUser = false)]
        [SwaggerOperation(Summary = "Mark invoice as seen by wallet", Tags = ["Invoice"])]
        public async Task<bool> SeenWalletAsync(
            InvoiceIdUpdate update)
        {
            return await _orderService.SeenWalletAsync(
                update,
                WalletAddress
            );
        }

        [HttpPost("[action]")]
        [Authorize(RequireActiveUser = false)]
        [SwaggerOperation(Summary = "Get Orders Reports", Tags = ["Order"])]
        public async  Task<OrderReportResult> GetOrderReportAsync()
        {
           return await _orderService.GetOrderReportAsync(WalletAddress);
        }
    }
}
