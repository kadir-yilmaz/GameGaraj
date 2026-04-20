namespace GameGaraj.WebUI.Models.Orders
{
    public class OrderViewModel
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
        public AddressViewModel Address { get; set; } = new();
        public List<OrderItemViewModel> OrderItems { get; set; } = new();
        public List<OrderPricingLedgerViewModel> OrderPricingLedgers { get; set; } = new();
        public int Status { get; set; }
    }

    public class OrderItemViewModel
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string PictureUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal DiscountAmount { get; set; }
    }

    public class OrderPricingLedgerViewModel
    {
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Type { get; set; } // 0=SubTotal, 1=Discount, 2=Fee, 3=Total
        public int SortOrder { get; set; }
    }

    public class AddressViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Neighborhood { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string AddressDetail { get; set; } = string.Empty;
    }

    public class OrderCreatedViewModel
    {
        public int OrderId { get; set; }
        public bool IsSuccessful { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public class OrderPricingSnapshot
    {
        public decimal OriginalTotalAmount { get; set; }
        public decimal CampaignDiscountAmount { get; set; }
        public decimal CouponDiscountAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public string? CouponCode { get; set; }
        public string? AppliedCampaignName { get; set; }
        public List<OrderPricingLedgerViewModel> OrderPricingLedgers { get; set; } = new();
    }
}
