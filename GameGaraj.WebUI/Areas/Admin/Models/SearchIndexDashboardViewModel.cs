using GameGaraj.WebUI.Models.Products;

namespace GameGaraj.WebUI.Areas.Admin.Models
{
    public class SearchIndexDashboardViewModel
    {
        public SearchIndexStatusViewModel? Status { get; set; }
        public SearchIndexDocumentPageViewModel Documents { get; set; } = new();
    }
}
