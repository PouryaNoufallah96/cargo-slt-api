using Microsoft.Extensions.DependencyInjection;
using Utilities.Services;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._Order._BackgroundServices
{
    public class RemoveNotRegisteredOrderAndInvoicesScheduler(IServiceProvider serviceProvider)
        : SchedulerBase(serviceProvider, TimeSpan.FromDays(1)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
        {
            var orderService = scopedProvider.GetRequiredService<IOrderService>();

            await orderService.RemoveNotRegisteredInvoicesAsync();
        }
    }
}