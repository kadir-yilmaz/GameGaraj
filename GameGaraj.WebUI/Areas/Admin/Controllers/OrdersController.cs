using GameGaraj.WebUI.Areas.Admin.Models;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class OrdersController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        public async Task<IActionResult> Index([FromQuery] OrderAdminIndexViewModel filter)
        {
            filter.StatusOptions = new List<SelectListItem>
            {
                new("Tum durumlar", string.Empty),
                new("Beklemede", "0"),
                new("Tamamlandi", "1"),
                new("Basarisiz", "2"),
                new("Hazirlaniyor", "3"),
                new("Kargoda", "4"),
                new("Teslim edildi", "5")
            };

            filter.Results = await _orderService.GetAdminOrdersPageAsync(
                filter.Query,
                filter.Status,
                filter.DateFrom,
                filter.DateTo,
                filter.Page,
                filter.PageSize);

            return View(filter);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, int status, string? returnUrl = null)
        {
            var result = await _orderService.UpdateOrderStatusAsync(orderId, status);

            TempData[result ? "Success" : "Error"] = result
                ? "Siparis durumu guncellendi."
                : "Siparis durumu guncellenemedi.";

            return RedirectToLocalOrIndex(returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> Ship(int orderId, string cargoCompany, string trackingNumber, string? returnUrl = null)
        {
            _logger.LogInformation("[Ship] Shipping order {OrderId} with {CargoCompany}, tracking: {TrackingNumber}", orderId, cargoCompany, trackingNumber);

            var result = await _orderService.ShipOrderAsync(orderId);

            TempData[result ? "Success" : "Error"] = result
                ? $"Siparis kargoya verildi. Kargo: {cargoCompany}, Takip No: {trackingNumber}"
                : "Siparis kargoya verilemedi.";

            return RedirectToLocalOrIndex(returnUrl);
        }

        private IActionResult RedirectToLocalOrIndex(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
