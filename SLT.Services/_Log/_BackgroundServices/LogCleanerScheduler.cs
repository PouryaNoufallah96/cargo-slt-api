using Microsoft.Extensions.DependencyInjection;
using Utilities.Services;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._Log._BackgroundServices
{
    public class LogCleanerScheduler(IServiceProvider serviceProvider)
        : SchedulerBase(serviceProvider, TimeSpan.FromDays(1)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
        {
            var logService = scopedProvider.GetRequiredService<ILogService>();

            await logService.HardDeleteLogsLogsAsync();
            await logService.HardDeleteRequestLogsAsync();
        }
    }
}