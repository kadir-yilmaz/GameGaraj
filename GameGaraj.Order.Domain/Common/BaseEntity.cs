namespace GameGaraj.Order.Domain.Common
{
    /// <summary>
    /// Tüm entity'lerin temel sınıfı
    /// </summary>
    public abstract class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
