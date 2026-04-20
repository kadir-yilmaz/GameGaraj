namespace GameGaraj.WebUI.Models.Campaigns
{
    public class ShippingSettingViewModel
    {
        public decimal FreeShippingThreshold { get; set; }
        public decimal DefaultShippingFee { get; set; }
        public bool IsActive { get; set; }
    }
}
