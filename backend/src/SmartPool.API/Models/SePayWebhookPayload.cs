namespace SmartPool.API.Models
{
    public class SePayWebhookPayload
    {
        public string? gateway { get; set; }
        public string? transactionDate { get; set; }
        public string? accountNumber { get; set; }
        public string? subAccount { get; set; }
        public decimal transferAmount { get; set; }
        public string? content { get; set; }
        public string? referenceCode { get; set; }
        public string? description { get; set; }
    }
}
