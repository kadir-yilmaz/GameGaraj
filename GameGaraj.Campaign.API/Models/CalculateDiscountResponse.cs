namespace GameGaraj.Campaign.API.Models
{
    /// <summary>
    /// İndirim hesaplama sonucu.
    /// Best Single Discount stratejisi ile yalnızca en avantajlı kural uygulanır.
    /// </summary>
    public class CalculateDiscountResponse
    {
        /// <summary>İndirim öncesi toplam tutar</summary>
        public decimal OriginalTotal { get; set; }

        /// <summary>Toplam indirim miktarı (TL)</summary>
        public decimal TotalDiscount { get; set; }

        /// <summary>İndirim sonrası ödenecek tutar</summary>
        public decimal FinalTotal { get; set; }

        /// <summary>Uygulanan kuralın ID'si (null ise indirim yok)</summary>
        public int? AppliedRuleId { get; set; }

        /// <summary>Uygulanan kuralın adı (Birleşik metin - Geriye dönük uyumluluk için)</summary>
        public string? AppliedRuleName { get; set; }

        /// <summary>Uygulanan tüm kuralların özeti</summary>
        public List<AppliedRuleSummary> AppliedRules { get; set; } = new();

        /// <summary>Ürün bazlı indirim detayları</summary>
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
}
