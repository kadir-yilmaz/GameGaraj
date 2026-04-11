using GameGaraj.WebUI.Models.Baskets;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers
{
    public class BasketController : Controller
    {
        private readonly IBasketService _basketService;
        private readonly ICatalogService _catalogService;

        public BasketController(IBasketService basketService, ICatalogService catalogService)
        {
            _basketService = basketService;
            _catalogService = catalogService;
        }

        public async Task<IActionResult> Index()
        {
            var basket = await _basketService.GetBasketAsync();
            return View(basket ?? new BasketViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> AddItem(string productId, int quantity = 1)
        {
            var product = await _catalogService.GetProductByIdAsync(productId);

            if (product == null)
                return NotFound();

            // Stok kontrolü: AvailableStock varsa onu kullan, yoksa Stock alanına bak (fallback)
            bool hasStock = (product.AvailableStock > 0) || (product.AvailableStock == 0 && product.Stock > 0);

            if (!hasStock)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Üzgünüz, bu ürünün stoğu tükendi." });
                }
                TempData["Error"] = "Üzgünüz, bu ürünün stoğu tükendi.";
                return RedirectToAction("Detail", "Product", new { id = productId });
            }

            var item = new BasketItemViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Price = product.Price,
                Quantity = quantity,
                ImageUrl = product.ImageUrls.FirstOrDefault() ?? ""
            };

            await _basketService.AddItemAsync(item);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var basket = await _basketService.GetBasketAsync();
                var count = basket?.Items.Sum(x => x.Quantity) ?? 0;
                return Json(new { success = true, message = "Ürün sepete eklendi!", count = count });
            }

            TempData["BasketNotification"] = "Ürün sepete eklendi!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RemoveItem(string productId)
        {
            await _basketService.RemoveItemAsync(productId);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(string productId, int quantity)
        {
            if (quantity <= 0)
            {
                await _basketService.RemoveItemAsync(productId);
            }
            else
            {
                var product = await _catalogService.GetProductByIdAsync(productId);
                if (product == null)
                    return NotFound();

                bool hasStock = (product.AvailableStock >= quantity) || (product.AvailableStock == 0 && product.Stock >= quantity);
                if (!hasStock)
                {
                    TempData["Error"] = "İstediğiniz miktarda stok bulunmamaktadır.";
                    return RedirectToAction(nameof(Index));
                }

                var item = new BasketItemViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = quantity,
                    ImageUrl = product.ImageUrls.FirstOrDefault() ?? ""
                };

                await _basketService.AddItemAsync(item);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Clear()
        {
            await _basketService.DeleteAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
