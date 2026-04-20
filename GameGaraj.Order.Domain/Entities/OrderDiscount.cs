using GameGaraj.Order.Domain.Common;

namespace GameGaraj.Order.Domain.Entities
{
    /// <summary>
    /// Siparişe uygulanan her bir indirimin (kampanyanın) detaylı kaydı.
    /// </summary>
    public class OrderDiscount : BaseEntity
    {
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;
        
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
