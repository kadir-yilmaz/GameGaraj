using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public async Task<IActionResult> Index()
        {
            var orders = await _orderService.GetAllOrdersAsync();
            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, int status)
        {
            var result = await _orderService.UpdateOrderStatusAsync(orderId, status);
            
            if (result)
            {
                TempData["Success"] = "Sipariş durumu güncellendi";
            }
            else
            {
                TempData["Error"] = "Sipariş durumu güncellenemedi";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Ship(int orderId, string cargoCompany, string trackingNumber)
        {
            _logger.LogInformation($"[Ship] Shipping order {orderId} with {cargoCompany}, tracking: {trackingNumber}");
            
            var result = await _orderService.ShipOrderAsync(orderId);
            
            if (result)
            {
                TempData["Success"] = $"Sipariş kargoya verildi. Kargo: {cargoCompany}, Takip No: {trackingNumber}";
            }
            else
            {
                TempData["Error"] = "Sipariş kargoya verilemedi";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
