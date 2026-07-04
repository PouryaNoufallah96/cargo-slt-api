using Utilities.DTOs;

namespace SLT.Services._Order.DTOs.Updates
{
    public class GetApprovalListUpdate
    {
        public Pagination Pagination { get; set; } = new Pagination();
        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    }

    public enum ApprovalStatus
    {
        Pending,
        Done
    }
}
