namespace GameGaraj.Shared.Events
{
    public class OrderStarted
    {
        public int OrderId { get; set; }
        public string BuyerId { get; set; } = string.Empty;
        public List<OrderItemMessage> OrderItems { get; set; } = new();
    }

    public class OrderItemMessage
    {
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
