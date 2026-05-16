using Microsoft.Extensions.DependencyInjection;
using Utilities.Services;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._Stake._BackgroundSerives
{
    public class RemoveNotRegisteredSTakeScheduler(IServiceProvider serviceProvider) : SchedulerBase(serviceProvider, TimeSpan.FromHours(3)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
        {
            var stakeService = scopedProvider.GetRequiredService<IStakeService>();
            await stakeService.RemoveNotRegisteredStakesAsync();
        }
    }
}
