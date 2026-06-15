namespace GameGaraj.WebUI.Areas.Admin.Models
{
    public class DashboardViewModel
    {
        public int ProductCount { get; set; }
        public int FeaturedProductCount { get; set; }
        public int CategoryCount { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public int LowStockProductCount { get; set; }
    }
}
