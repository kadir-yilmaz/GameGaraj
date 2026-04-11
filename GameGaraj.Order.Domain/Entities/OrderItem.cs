using GameGaraj.Order.Domain.Common;

namespace GameGaraj.Order.Domain.Entities
{
    /// <summary>
    /// Sipariş kalemi (PC Ürünleri)
    /// </summary>
    public class OrderItem : BaseEntity
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string PictureUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }

        // Navigation property
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;
    }
}
