namespace GameGaraj.Order.Application.Dtos
{
    public class OrderItemDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string PictureUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
