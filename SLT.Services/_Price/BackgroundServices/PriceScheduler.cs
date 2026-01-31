using Utilities.Services;
using Microsoft.Extensions.DependencyInjection;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._Price.BackgroundServices
{
    public class PriceScheduler(IServiceProvider serviceProvider) : SchedulerBase(serviceProvider, TimeSpan.FromMinutes(3)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
        {
            var priceService = scopedProvider.GetRequiredService<IPriceService>();
            await priceService.FetchAllPricesAsync();
        }
    }
}
