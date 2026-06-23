using System.Text.Json.Serialization;

namespace GameGaraj.WebUI.Models.Campaigns
{
    public class CouponViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("CouponType")]
        public string DiscountType { get; set; } = string.Empty; // FixedAmount, Percentage, FreeShipping

        public decimal? Amount { get; set; }

        [JsonPropertyName("Rate")]
        public decimal? DiscountRate { get; set; }

        public decimal? MaxDiscountAmount { get; set; }

        [JsonPropertyName("MinOrderAmount")]
        public decimal? MinPurchaseAmount { get; set; }

        public string? UserId { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedDate { get; set; }

        [JsonPropertyName("ExpirationDate")]
        public DateTime? ExpiryDate { get; set; }

        [JsonPropertyName("CreatedTime")]
        public DateTime CreatedDate { get; set; }

        public string DiscountTypeDisplayName => DiscountType switch
        {
            "FixedAmount" => "Sabit Tutar İndirimi",
            "Percentage" => "Yüzdelik İndirim",
            "FreeShipping" => "Kargo Ücretsiz",
            _ => DiscountType
        };
    }

    public class CouponCreateInput
    {
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("CouponType")]
        public string DiscountType { get; set; } = "FixedAmount";

        public decimal? Amount { get; set; }

        [JsonPropertyName("Rate")]
        public decimal? DiscountRate { get; set; }

        public decimal? MaxDiscountAmount { get; set; }

        [JsonPropertyName("MinOrderAmount")]
        public decimal? MinPurchaseAmount { get; set; }

        public string? UserId { get; set; }

        [JsonPropertyName("ExpirationDate")]
        public DateTime? ExpiryDate { get; set; }
    }

    public class CouponRewardRuleViewModel
    {
        public int Id { get; set; }

        [JsonPropertyName("Name")]
        public string RuleName { get; set; } = string.Empty;

        public string? Description { get; set; }

        [JsonPropertyName("MinSpendAmount")]
        public decimal RequiredSpendAmount { get; set; }

        [JsonPropertyName("SpendPeriodDays")]
        public int PeriodInDays { get; set; }

        [JsonPropertyName("RewardAmount")]
        public decimal? RewardCouponAmount { get; set; }

        [JsonPropertyName("RewardRate")]
        public decimal? RewardCouponRate { get; set; }

        [JsonPropertyName("RewardMaxDiscount")]
        public decimal? RewardCouponMaxAmount { get; set; }

        public string RewardCouponType { get; set; } = "FixedAmount"; // FixedAmount, Percentage, FreeShipping
        
        public int RewardValidDays { get; set; } = 30;
        
        public bool IsActive { get; set; }

        [JsonPropertyName("CreatedTime")]
        public DateTime CreatedDate { get; set; }

        public string RewardTypeDisplayName => RewardCouponType switch
        {
            "FixedAmount" => "Sabit Tutar Kuponu",
            "Percentage" => "Yüzdelik Kupon",
            "FreeShipping" => "Kargo Bedava Kuponu",
            _ => RewardCouponType
        };
    }

    public class CouponRewardRuleCreateInput
    {
        [JsonPropertyName("Name")]
        public string RuleName { get; set; } = string.Empty;

        public string? Description { get; set; }

        [JsonPropertyName("MinSpendAmount")]
        public decimal RequiredSpendAmount { get; set; }

        [JsonPropertyName("SpendPeriodDays")]
        public int PeriodInDays { get; set; }

        [JsonPropertyName("RewardAmount")]
        public decimal? RewardCouponAmount { get; set; }

        [JsonPropertyName("RewardRate")]
        public decimal? RewardCouponRate { get; set; }

        [JsonPropertyName("RewardMaxDiscount")]
        public decimal? RewardCouponMaxAmount { get; set; }

        public string RewardCouponType { get; set; } = "FixedAmount";
        
        public int RewardValidDays { get; set; } = 30;
        
        public bool IsActive { get; set; } = true;
    }

    public class NotificationViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }

        [JsonPropertyName("CreatedTime")]
        public DateTime CreatedDate { get; set; }
    }
}
