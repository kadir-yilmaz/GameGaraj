using GameGaraj.WebUI.Models.Common;
using GameGaraj.WebUI.Models.Products;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameGaraj.WebUI.Areas.Admin.Models
{
    public class ProductAdminIndexViewModel
    {
        public string? Query { get; set; }
        public string? CategoryId { get; set; }
        public bool? IsFeatured { get; set; }
        public bool? IsActive { get; set; }
        public string? StockState { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public List<SelectListItem> CategoryOptions { get; set; } = new();
        public PagedResultViewModel<ProductViewModel> Results { get; set; } = new();
    }
}
