using GameGaraj.WebUI.Areas.Admin.Models;
using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin, editor")]
    public class DashboardController : Controller
    {
        private readonly ILogger<DashboardController> _logger;
        private readonly ICatalogService _catalogService;
        private readonly IOrderService _orderService;

        public DashboardController(
            ILogger<DashboardController> logger,
            ICatalogService catalogService,
            IOrderService orderService)
        {
            _logger = logger;
            _catalogService = catalogService;
            _orderService = orderService;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("[Admin Dashboard] Dashboard accessed");

            var products = await _catalogService.GetAllProductsAsync();
            var categories = await _catalogService.GetAllCategoriesAsync();
            var orders = User.IsInRole("admin")
                ? await _orderService.GetAllOrdersAsync()
                : new();

            var model = new DashboardViewModel
            {
                ProductCount = products.Count,
                FeaturedProductCount = products.Count(product => product.IsFeatured),
                CategoryCount = CountCategories(categories),
                OrderCount = orders.Count,
                TotalRevenue = orders.Sum(order => order.TotalPaidAmount),
                LowStockProductCount = products.Count(product => product.AvailableStock <= 5)
            };

            return View(model);
        }

        private static int CountCategories(IEnumerable<CategoryViewModel> categories)
        {
            var count = 0;

            foreach (var category in categories)
            {
                count++;
                if (category.Children != null && category.Children.Any())
                {
                    count += CountCategories(category.Children);
                }
            }

            return count;
        }
    }
}
