using System.ComponentModel.DataAnnotations;

namespace GameGaraj.WebUI.Models.Campaigns
{
    public class CampaignRuleViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string RuleType { get; set; } = string.Empty;
        public string? CategoryId { get; set; }
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? BrandName { get; set; }
        public decimal? MinAmount { get; set; }
        public int? MinQuantity { get; set; }
        public int? FreeQuantity { get; set; }
        public decimal? DiscountRate { get; set; }
        public decimal? FixedDiscount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedTime { get; set; }

        /// <summary>Kural türünün Türkçe görüntü adı</summary>
        public string RuleTypeDisplayName => RuleType switch
        {
            "TotalAmount" => "Toplam Tutar İndirimi",
            "BuyXGetYFree" => "X Al Y Bedava",
            "CheapestItemDiscount" => "En Ucuz Ürüne İndirim",
            "BrandDiscount" => "Seçili Ürün/Marka/Kategori İndirimi",
            _ => RuleType
        };
    }

    public class CampaignRuleCreateInput
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string RuleType { get; set; } = string.Empty;
        public string? CategoryId { get; set; }
        public string? ProductId { get; set; }
        public string? BrandName { get; set; }
        public decimal? MinAmount { get; set; }
        public int? MinQuantity { get; set; }
        public int? FreeQuantity { get; set; }
        [System.ComponentModel.DataAnnotations.Range(0, 100, ErrorMessage = "İndirim oranı %0 ile %100 arasında olmalıdır.")]
        public decimal? DiscountRate { get; set; }
        public decimal? FixedDiscount { get; set; }
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? StartDate { get; set; }
        
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? EndDate { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CampaignRuleUpdateInput
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string RuleType { get; set; } = string.Empty;
        public string? CategoryId { get; set; }
        public string? ProductId { get; set; }
        public string? BrandName { get; set; }
        public decimal? MinAmount { get; set; }
        public int? MinQuantity { get; set; }
        public int? FreeQuantity { get; set; }
        [System.ComponentModel.DataAnnotations.Range(0, 100, ErrorMessage = "İndirim oranı %0 ile %100 arasında olmalıdır.")]
        public decimal? DiscountRate { get; set; }
        public decimal? FixedDiscount { get; set; }
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? StartDate { get; set; }
        
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? EndDate { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
    }

    public class CalculateDiscountResponse
    {
        public decimal OriginalTotal { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal FinalTotal { get; set; }
        public string? AppliedRuleName { get; set; }
        public List<AppliedRuleSummary> AppliedRules { get; set; } = new();
        public List<DiscountDetail> Details { get; set; } = new();
        public bool IsCouponApplied { get; set; }
        public string? CouponMessage { get; set; }
    }

    public class AppliedRuleSummary
    {
        public int RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
    }

    public class DiscountDetail
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal OriginalLineTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DiscountedLineTotal { get; set; }
        public string? RuleName { get; set; }
    }

    public class CalculateDiscountRequest
    {
        public List<OrderItemDto> Items { get; set; } = new();
        public string? CouponCode { get; set; }
        public string? UserId { get; set; }
    }

    public class OrderItemDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}
