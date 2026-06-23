namespace GameGaraj.Campaign.API.Models
{
    /// <summary>
    /// Kullanıcının alışveriş geçmişi kaydı.
    /// Kupon Kazan sistemi bu tabloyu kullanarak belirli süre içindeki
    /// toplam alışveriş tutarını hesaplar.
    /// Order API'ye bağımlılık oluşturmamak için Campaign DB'de tutulur.
    /// </summary>
    public class UserPurchaseLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    }
}
