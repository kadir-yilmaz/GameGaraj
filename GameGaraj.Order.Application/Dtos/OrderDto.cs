namespace GameGaraj.Order.Application.Dtos
{
    public class OrderDto
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public string BuyerId { get; set; } = string.Empty;
        public decimal OriginalTotalAmount { get; set; }
        public decimal CampaignDiscountAmount { get; set; }
        public decimal CouponDiscountAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public string? CouponCode { get; set; }
        public string? AppliedCampaignName { get; set; }
        public AddressDto Address { get; set; } = new();
        public List<OrderItemDto> OrderItems { get; set; } = new();
        public List<OrderPricingLedgerDto> OrderPricingLedgers { get; set; } = new();
        public int Status { get; set; }
    }

    public class OrderPricingLedgerDto
    {
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Type { get; set; }
        public int SortOrder { get; set; }
    }
}
