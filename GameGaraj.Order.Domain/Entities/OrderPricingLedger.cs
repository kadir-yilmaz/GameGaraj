using GameGaraj.Order.Domain.Common;

namespace GameGaraj.Order.Domain.Entities
{
    /// <summary>
    /// Siparişin detaylı fiyat dökümü (Hesap Özeti Satırı).
    /// </summary>
    public class OrderPricingLedger : BaseEntity
    {
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;

        /// <summary>
        /// Satır başlığı (Örn: Ara Toplam, İndirim, Kargo, Net Toplam)
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Satır tutarı
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Kayıt tipi (0=Bilgi/Toplam, 1=İndirim, 2=Ücret/Fee, 3=Net Sonuç)
        /// </summary>
        public LedgerRowType Type { get; set; }

        /// <summary>
        /// Arayüzde görünme sırası
        /// </summary>
        public int SortOrder { get; set; }
    }

    public enum LedgerRowType
    {
        SubTotal = 0,
        Discount = 1,
        Fee = 2,
        TransactionTotal = 3
    }
}
