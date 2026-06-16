using GameGaraj.WebUI.Models.Common;
using GameGaraj.WebUI.Models.Orders;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameGaraj.WebUI.Areas.Admin.Models
{
    public class OrderAdminIndexViewModel
    {
        public string? Query { get; set; }
        public int? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;

        public List<SelectListItem> StatusOptions { get; set; } = new();
        public PagedResultViewModel<OrderViewModel> Results { get; set; } = new();
    }
}
