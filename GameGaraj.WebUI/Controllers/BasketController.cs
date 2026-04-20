using GameGaraj.WebUI.Models.Baskets;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers
{
    public class BasketController : Controller
    {
        private readonly IBasketService _basketService;
        private readonly ICatalogService _catalogService;
        private readonly ICampaignService _campaignService;
        private readonly IDiscountService _discountService;

        public BasketController(IBasketService basketService, ICatalogService catalogService, ICampaignService campaignService, IDiscountService discountService)
        {
            _basketService = basketService;
            _catalogService = catalogService;
            _campaignService = campaignService;
            _discountService = discountService;
        }

        public async Task<IActionResult> Index()
        {
            var basket = await _basketService.GetBasketAsync() ?? new BasketViewModel();
            
            if (basket.Items.Any())
            {
                // Legacy Basket Healing: Önceden eklenmiş ürünlerde CategoryId eksikse Catalog'dan tamamla
                bool needsHealing = false;
                foreach (var item in basket.Items)
                {
                    if (string.IsNullOrEmpty(item.CategoryId))
                    {
                        var product = await _catalogService.GetProductByIdAsync(item.ProductId);
                        if (product != null)
                        {
                            item.CategoryId = product.CategoryId;
                            needsHealing = true;
                        }
                    }
                }
                
                // Eğer eksik kategori tamamlandıysa Redis'teki sepeti güncelle
                if (needsHealing)
                {
                    await _basketService.SaveOrUpdateAsync(basket);
                }

                try
                {
                    var discountRequest = new GameGaraj.WebUI.Models.Campaigns.CalculateDiscountRequest
                    {
                        Items = basket.Items.Select(i => new GameGaraj.WebUI.Models.Campaigns.OrderItemDto
                        {
                            ProductId = i.ProductId,
                            ProductName = i.ProductName,
                            CategoryId = i.CategoryId,
                            UnitPrice = i.Price,
                            Quantity = i.Quantity
                        }).ToList()
                    };

                    var discountResult = await _campaignService.CalculateDiscountAsync(discountRequest);
                    ViewBag.DiscountResult = discountResult;
                }
                catch (Exception)
                {
                    // Kampanya servisi ulaşılamazsa çökmeyi önle
                }
            }
            
            // Kargo ayarlarını çek ve görüntüye passla
            var shippingSetting = await _campaignService.GetShippingSettingAsync();
            if (shippingSetting == null)
            {
                shippingSetting = new GameGaraj.WebUI.Models.Campaigns.ShippingSettingViewModel
                {
                    FreeShippingThreshold = 500,
                    DefaultShippingFee = 0,
                    IsActive = false
                };
            }
            ViewBag.ShippingSetting = shippingSetting;

            // Kupon hesaplama
            var couponJson = HttpContext.Session.GetString("AppliedCoupon");
            if (!string.IsNullOrEmpty(couponJson))
            {
                var coupon = System.Text.Json.JsonSerializer.Deserialize<GameGaraj.WebUI.Models.Discounts.DiscountViewModel>(couponJson);
                ViewBag.AppliedCoupon = coupon;
            }

            return View(basket);
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
                CategoryId = product.CategoryId, // Map Category
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
                    CategoryId = product.CategoryId, // Map Category
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

        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(string couponCode)
        {
            if (string.IsNullOrEmpty(couponCode))
            {
                TempData["CouponError"] = "Lütfen bir kupon kodu girin.";
                return RedirectToAction(nameof(Index));
            }

            var discount = await _discountService.GetByCodeAsync(couponCode);
            if (discount == null)
            {
                TempData["CouponError"] = "Geçersiz veya süresi dolmuş kupon kodu.";
                return RedirectToAction(nameof(Index));
            }

            HttpContext.Session.SetString("AppliedCoupon", System.Text.Json.JsonSerializer.Serialize(discount));
            TempData["CouponSuccess"] = $"'{couponCode}' kuponu başarıyla uygulandı!";
            
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RemoveCoupon()
        {
            HttpContext.Session.Remove("AppliedCoupon");
            return RedirectToAction(nameof(Index));
        }
    }
}
