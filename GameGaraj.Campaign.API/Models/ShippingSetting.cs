namespace GameGaraj.Campaign.API.Models
{
    public class ShippingSetting
    {
        public int Id { get; set; }
        public decimal FreeShippingThreshold { get; set; }
        public decimal DefaultShippingFee { get; set; }
        public bool IsActive { get; set; }
    }
}
