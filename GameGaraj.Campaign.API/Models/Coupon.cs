namespace GameGaraj.Campaign.API.Models
{
    /// <summary>
    /// Kupon tanımı. 3 tip desteklenir: FixedAmount (sabit tutar), Percentage (yüzdelik), FreeShipping (ücretsiz kargo).
    /// Herkese açık (UserId = null) veya kullanıcıya özel olabilir.
    /// Bir kupon yalnızca 1 kez kullanılabilir.
    /// </summary>
    public class Coupon
    {
        public int Id { get; set; }

        /// <summary>Benzersiz kupon kodu (KAZAN50, HOSGELDIN100 vb.)</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>null = herkese açık, dolu = kullanıcıya özel</summary>
        public string? UserId { get; set; }

        /// <summary>Kupon tipi: "FixedAmount", "Percentage", "FreeShipping"</summary>
        public string CouponType { get; set; } = string.Empty;

        /// <summary>Sabit tutar indirimi (ör: 50 TL)</summary>
        public decimal? Amount { get; set; }

        /// <summary>Yüzdelik indirim oranı (ör: 20 = %20)</summary>
        public decimal? Rate { get; set; }

        /// <summary>Yüzdelik indirimde maksimum indirim tutarı sınırı</summary>
        public decimal? MaxDiscountAmount { get; set; }

        /// <summary>Minimum sepet tutarı (alt limit)</summary>
        public decimal? MinOrderAmount { get; set; }

        /// <summary>Kupon kullanıldı mı? Bir kupon yalnızca 1 kez kullanılabilir.</summary>
        public bool IsUsed { get; set; } = false;

        /// <summary>Kuponun aktif olup olmadığı</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Kupon Kazan sistemiyle kazanıldı mı?</summary>
        public bool IsEarnedReward { get; set; } = false;

        /// <summary>Hangi ödül kuralından kazanıldı? (opsiyonel)</summary>
        public int? RewardRuleId { get; set; }

        /// <summary>Son kullanma tarihi (null = süresiz)</summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>Kupon diğer kampanyalarla birleştirilebilir mi?</summary>
        public bool AllowWithOtherCampaigns { get; set; } = false;

        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    }
}
