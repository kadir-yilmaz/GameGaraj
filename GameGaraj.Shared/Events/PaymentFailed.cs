namespace GameGaraj.Shared.Events
{
    /// <summary>
    /// Ödeme başarısız olduğunda yayınlanan event
    /// </summary>
    public class PaymentFailed
    {
        public int OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<OrderItemMessage> OrderItems { get; set; } = new();
    }
}
