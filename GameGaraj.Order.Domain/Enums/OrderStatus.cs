namespace GameGaraj.Order.Domain.Enums
{
    public enum OrderStatus
    {
        /// <summary>
        /// Sipariş oluşturuldu, ödeme bekleniyor
        /// </summary>
        Pending = 0,
        
        /// <summary>
        /// Ödeme tamamlandı, sipariş onaylandı
        /// </summary>
        Completed = 1,
        
        /// <summary>
        /// Ödeme başarısız, sipariş iptal edildi
        /// </summary>
        Failed = 2,
        
        /// <summary>
        /// Sipariş hazırlanıyor
        /// </summary>
        Processing = 3,
        
        /// <summary>
        /// Kargoya verildi
        /// </summary>
        Shipped = 4,
        
        /// <summary>
        /// Teslim edildi
        /// </summary>
        Delivered = 5
    }
}
