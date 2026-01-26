using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using SLT.Services._Log.DTOs.Updates;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._Log
{
    public class LogService(ILogRepository _logRepository, IRequestLogRepository _requestLogRepository)
        : ILogService, IScopedDependency
    {
        public async Task CaptureLogAsync(LogUpdate update)
        {
            var newLog = new Log
            {
                Message = update.Message,
                FullName = update.FullName,
                PublicKey = update.PublicKey,
                ServiceName = update.ServiceName,
                MethodName = update.MethodName,
                Importance = update.Importance,
            };

            await _logRepository.InsertOneAsync(newLog);
        }

        public async Task CaptureRequestLogAsync(RequestLogUpdate update,string publicKey, string walletAddress)
        {
            var newLog = new RequestLog
            {
                ControllerName = update.ControllerName,
                ApiName = update.ApiName,
                Body = update.Body,
                ClientIP = update.ClientIP,
                Headers = update.Headers,
                Query = update.Query,
                RoutePath = update.RoutePath,
                WalletAddress = walletAddress,
                PublicKey = publicKey
            };

            await _requestLogRepository.InsertOneAsync(newLog);
        }

        public async Task HardDeleteLogsLogsAsync()
        {
            await _logRepository.RealDeleteManyAsync(q => q.CreatedMoment <= DateTime.UtcNow.AddMonths(-1));
        }

        public async Task HardDeleteRequestLogsAsync()
        {
            await _requestLogRepository.RealDeleteManyAsync(q => q.CreatedMoment <= DateTime.UtcNow.AddDays(-10));
        }


    }
}
