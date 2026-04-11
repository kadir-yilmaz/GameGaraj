namespace GameGaraj.Shared.Events
{
    /// <summary>
    /// Ödeme başarılı olduğunda yayınlanan event
    /// </summary>
    public class PaymentCompleted
    {
        public int OrderId { get; set; }
        public List<OrderItemMessage> OrderItems { get; set; } = new();
    }
}
