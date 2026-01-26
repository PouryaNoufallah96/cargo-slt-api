using SLT.Services._Log.DTOs.Updates;

namespace SLT.Services._Log
{
    public interface ILogService
    {
        Task CaptureLogAsync(LogUpdate update);
        Task CaptureRequestLogAsync(RequestLogUpdate update, string publicKey, string walletAddress);
        Task HardDeleteRequestLogsAsync();
        Task HardDeleteLogsLogsAsync();
    }
}
