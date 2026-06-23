namespace GameGaraj.Campaign.API.Models
{
    /// <summary>
    /// Kullanıcıya gönderilen bildirim. Şu an kupon kazanma bildirimleri için kullanılır.
    /// </summary>
    public class UserNotification
    {
        public int Id { get; set; }

        /// <summary>Bildirim sahibi kullanıcı</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Bildirim başlığı: "Tebrikler! Kupon Kazandınız 🎉"</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Bildirim detay mesajı</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>İkon sınıfı (ör: "fas fa-gift")</summary>
        public string? IconClass { get; set; }

        /// <summary>Tıklama linki (ör: "/Order/MyCoupons")</summary>
        public string? LinkUrl { get; set; }

        /// <summary>Okundu mu?</summary>
        public bool IsRead { get; set; } = false;

        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    }
}
