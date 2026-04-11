namespace GameGaraj.Discount.API.Models
{
    [Dapper.Contrib.Extensions.Table("discount")]
    public class Discount
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int Rate { get; set; }
        public string Code { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public DateTime? ExpirationDate { get; set; }
        
        /// <summary>
        /// Comma separated product ids
        /// </summary>
        public string? AllowedProductIds { get; set; }
    }
}
