using GameGaraj.Order.Domain.Common;

namespace GameGaraj.Order.Domain.Entities
{
    public class Order : BaseEntity
    {
        public DateTime CreatedDate { get; set; }
        public string BuyerId { get; set; } = string.Empty;

        // Fiyatlandırma Verileri (Hızlı listeleme için özet kolonlar korunuyor)
        public decimal OriginalTotalAmount { get; set; }
        public decimal CampaignDiscountAmount { get; set; }
        public decimal CouponDiscountAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal TotalPaidAmount { get; set; }

        public string? CouponCode { get; set; }
        public string? AppliedCampaignName { get; set; }

        public Address DeliveryAddress { get; set; } = null!;
        public Address InvoiceAddress { get; set; } = null!;

        public List<OrderItem> OrderItems { get; set; } = new();
        
        /// <summary>
        /// Siparişin detaylı mali özeti (Ledger)
        /// </summary>
        public List<OrderPricingLedger> OrderPricingLedgers { get; set; } = new();

        public int Status { get; set; } // 0=Pending, 1=Completed, 2=Failed, 3=Preparing, 4=Shipped, 5=Delivered
    }
}
