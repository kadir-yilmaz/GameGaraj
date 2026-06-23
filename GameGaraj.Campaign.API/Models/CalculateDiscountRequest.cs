namespace GameGaraj.Campaign.API.Models
{
    /// <summary>
    /// Sepetteki ürünleri temsil eden istek modeli.
    /// WebUI sepet bilgisini bu formatta gönderir.
    /// </summary>
    public class CalculateDiscountRequest
    {
        public List<OrderItem> Items { get; set; } = new();
        public string? CouponCode { get; set; }
        public string? UserId { get; set; }
    }

    public class OrderItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}
