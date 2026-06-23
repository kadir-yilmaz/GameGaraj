namespace GameGaraj.Campaign.API.Models
{
    /// <summary>
    /// Kupon Kazan kuralı. Admin tarafından tanımlanır.
    /// Kullanıcı belirli süre içinde belirli tutarda alışveriş yaparsa kupon kazanır.
    /// Örnek: "30 gün içinde 500 TL alışveriş yap → 100 TL kupon kazan"
    /// </summary>
    public class CouponRewardRule
    {
        public int Id { get; set; }

        /// <summary>Kural adı: "500 TL Alışverişe 100 TL Kupon"</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Admin açıklaması</summary>
        public string? Description { get; set; }

        /// <summary>Minimum toplam alışveriş tutarı (ör: 500 TL)</summary>
        public decimal MinSpendAmount { get; set; }

        /// <summary>Kaç gün içinde bu tutara ulaşılmalı (ör: 30 gün)</summary>
        public int SpendPeriodDays { get; set; }

        /// <summary>Kazanılacak kuponun tipi: "FixedAmount", "Percentage"</summary>
        public string RewardCouponType { get; set; } = string.Empty;

        /// <summary>Kazanılacak sabit tutar (ör: 100 TL)</summary>
        public decimal? RewardAmount { get; set; }

        /// <summary>Kazanılacak yüzdelik oran (ör: 20 = %20)</summary>
        public decimal? RewardRate { get; set; }

        /// <summary>Kazanılacak kuponun max indirim sınırı</summary>
        public decimal? RewardMaxDiscount { get; set; }

        /// <summary>Kazanılacak kuponun minimum sepet tutarı</summary>
        public decimal? RewardMinOrderAmount { get; set; }

        /// <summary>Kazanılacak kuponun geçerlilik süresi (gün)</summary>
        public int RewardValidDays { get; set; } = 30;

        /// <summary>Kuralın aktif olup olmadığı</summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    }
}
