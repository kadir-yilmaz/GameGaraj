using GameGaraj.Order.Domain.Common;
using GameGaraj.Order.Domain.Enums;

namespace GameGaraj.Order.Domain.Entities
{
    /// <summary>
    /// Sipariş entity'si
    /// </summary>
    public class Order : BaseEntity
    {
        public string BuyerId { get; set; } = string.Empty;
        
        /// <summary>
        /// Sipariş durumu (Pending, Completed, Failed)
        /// </summary>
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        // Navigation properties
        public int DeliveryAddressId { get; set; }
        public Address DeliveryAddress { get; set; } = null!;
        
        public int? InvoiceAddressId { get; set; }
        public Address? InvoiceAddress { get; set; }
        
        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        /// <summary>
        /// Toplam sipariş tutarını hesaplar
        /// </summary>
        public decimal GetTotalPrice => OrderItems.Sum(x => x.Price);
    }
}
