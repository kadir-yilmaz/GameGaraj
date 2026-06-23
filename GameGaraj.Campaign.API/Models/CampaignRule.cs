namespace GameGaraj.Campaign.API.Models
{
    /// <summary>
    /// Veritabanında saklanan indirim kuralı tanımı.
    /// Admin panelden CRUD ile yönetilir.
    /// </summary>
    public class CampaignRule
    {
        public int Id { get; set; }

        /// <summary>Kural adı – "1000 TL Üzeri %10 İndirim"</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Admin tarafından girilen açıklama</summary>
        public string? Description { get; set; }

        /// <summary>Strateji tipi: TotalAmount, BuyXGetYFree, CheapestItemDiscount</summary>
        public string RuleType { get; set; } = string.Empty;

        /// <summary>Kategori bazlı kurallar için (null ise tüm kategoriler)</summary>
        public string? CategoryId { get; set; }

        /// <summary>Ürün bazlı kurallar için (null ise tüm ürünler / kategori bazlı)</summary>
        public string? ProductId { get; set; }

        /// <summary>TotalAmount kuralı için minimum sipariş tutarı</summary>
        public decimal? MinAmount { get; set; }

        /// <summary>Minimum adet (BuyXGetYFree: X adet, CheapestItem: min 2)</summary>
        public int? MinQuantity { get; set; }

        /// <summary>BuyXGetYFree kuralında bedava verilecek adet</summary>
        public int? FreeQuantity { get; set; }

        /// <summary>Yüzde indirim oranı (örn: 10 = %10)</summary>
        public decimal? DiscountRate { get; set; }

        /// <summary>Sabit tutar indirimi (ör: sepet 1000 TL → 100 TL indirim)</summary>
        public decimal? FixedDiscount { get; set; }

        /// <summary>Marka bazlı kampanyalar için (ör: "Samsung")</summary>
        public string? BrandName { get; set; }

        /// <summary>Kampanya başlangıç tarihi (null ise hemen aktif)</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>Kampanya bitiş tarihi (null ise süresiz)</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>Kampanya banner görseli URL'i (opsiyonel)</summary>
        public string? ImageUrl { get; set; }



        /// <summary>Kuralın aktif olup olmadığı</summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    }
}
