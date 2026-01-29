namespace SLT.Services._BlockChain.DTOs.Updates
{
    public class CreateMultipleInvoicesUpdate
    {
        public string Id { get; set; }
        public string TokenAddress { get; set; }
        public decimal USDTAmount { get; set; }
        public DateTime UnLockTime { get; set; }
    } 
}
 