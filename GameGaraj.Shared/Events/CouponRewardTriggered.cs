namespace GameGaraj.Shared.Events
{
    /// <summary>
    /// Ödeme başarılı olarak sipariş tamamlandığında, kupon kazanma kurallarını tetiklemek için yayınlanan event.
    /// </summary>
    public class CouponRewardTriggered
    {
        public int OrderId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PurchaseDate { get; set; }
    }
}
