namespace GameGaraj.WebUI.Models.Discounts
{
    public class DiscountViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int Rate { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? AllowedProductIds { get; set; }
    }

    public class CreateDiscountInput
    {
        public string? UserId { get; set; }
        public int Rate { get; set; }
        public decimal Amount { get; set; }
        public string Code { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime? ExpirationDate { get; set; }
        public string? AllowedProductIds { get; set; }
    }

    public class UpdateDiscountInput
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public int Rate { get; set; }
        public decimal Amount { get; set; }
        public string Code { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? AllowedProductIds { get; set; }
    }
}
