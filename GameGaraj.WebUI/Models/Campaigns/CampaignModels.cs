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
        public decimal? MinAmount { get; set; }
        public int? MinQuantity { get; set; }
        public int? FreeQuantity { get; set; }
        public decimal? DiscountRate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedTime { get; set; }

        /// <summary>Kural türünün Türkçe görüntü adı</summary>
        public string RuleTypeDisplayName => RuleType switch
        {
            "TotalAmount" => "Toplam Tutar İndirimi",
            "BuyXGetYFree" => "X Al Y Bedava",
            "CheapestItemDiscount" => "En Ucuz Ürüne İndirim",
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
        public decimal? MinAmount { get; set; }
        public int? MinQuantity { get; set; }
        public int? FreeQuantity { get; set; }
        public decimal? DiscountRate { get; set; }
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
        public decimal? MinAmount { get; set; }
        public int? MinQuantity { get; set; }
        public int? FreeQuantity { get; set; }
        public decimal? DiscountRate { get; set; }
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
    }

    public class CalculateDiscountRequest
    {
        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}
