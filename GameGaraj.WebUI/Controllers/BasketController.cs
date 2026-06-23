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

        public BasketController(IBasketService basketService, ICatalogService catalogService, ICampaignService campaignService)
        {
            _basketService = basketService;
            _catalogService = catalogService;
            _campaignService = campaignService;
        }

        public async Task<IActionResult> Index()
        {
            var basket = await _basketService.GetBasketAsync() ?? new BasketViewModel();
            
            if (basket.Items.Any())
            {
                // Basket Healing & Price Sync: Update Price, CategoryId, and Brand from Catalog
                bool needsHealing = false;
                foreach (var item in basket.Items)
                {
                    if (string.IsNullOrEmpty(item.ProductId))
                    {
                        continue;
                    }

                    var product = await _catalogService.GetProductByIdAsync(item.ProductId);
                    if (product != null)
                    {
                        if (item.Price != product.Price)
                        {
                            item.Price = product.Price;
                            needsHealing = true;
                        }
                        if (string.IsNullOrEmpty(item.CategoryId) && !string.IsNullOrEmpty(product.CategoryId))
                        {
                            item.CategoryId = product.CategoryId;
                            needsHealing = true;
                        }
                        if (string.IsNullOrEmpty(item.Brand) && !string.IsNullOrEmpty(product.Brand))
                        {
                            item.Brand = product.Brand;
                            needsHealing = true;
                        }
                    }
                }
                
                // Eğer değişiklik olduysa Redis'teki sepeti güncelle
                if (needsHealing)
                {
                    await _basketService.SaveOrUpdateAsync(basket);
                }

                // Kupon Kodu Session'dan Al
                var couponCode = HttpContext.Session.GetString("AppliedCouponCode");
                var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                try
                {
                    var discountRequest = new GameGaraj.WebUI.Models.Campaigns.CalculateDiscountRequest
                    {
                        Items = basket.Items.Select(i => new GameGaraj.WebUI.Models.Campaigns.OrderItemDto
                        {
                            ProductId = i.ProductId,
                            ProductName = i.ProductName,
                            CategoryId = i.CategoryId,
                            Brand = i.Brand,
                            UnitPrice = i.Price,
                            Quantity = i.Quantity
                        }).ToList(),
                        CouponCode = couponCode,
                        UserId = currentUserId
                    };

                    var discountResult = await _campaignService.CalculateDiscountAsync(discountRequest);
                    ViewBag.DiscountResult = discountResult;

                    // Eğer API kuponun geçersiz olduğunu söylerse (IsCouponApplied = false ve CouponCode gönderilmişse) 
                    // Session'ı temizle ve hatayı göster
                    if (!string.IsNullOrEmpty(couponCode) && discountResult != null && !discountResult.IsCouponApplied)
                    {
                        HttpContext.Session.Remove("AppliedCouponCode");
                        TempData["CouponError"] = discountResult.CouponMessage ?? "Kupon geçersiz.";
                    }
                    else if (!string.IsNullOrEmpty(couponCode) && discountResult != null && discountResult.IsCouponApplied)
                    {
                        TempData["CouponSuccess"] = discountResult.CouponMessage ?? "Kupon uygulandı.";
                    }
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
                Brand = product.Brand, // Map Brand
                Price = product.Price,
                Quantity = quantity,
                ImageUrl = product.FirstImageUrl,
                ProductSlug = product.Slug
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
                    Brand = product.Brand, // Map Brand
                    Price = product.Price,
                    Quantity = quantity,
                    ImageUrl = product.FirstImageUrl,
                    ProductSlug = product.Slug
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
        public IActionResult ApplyCoupon([FromForm] string couponCode)
        {
            if (string.IsNullOrEmpty(couponCode))
            {
                return Json(new { success = false, message = "Lütfen bir kupon kodu girin." });
            }

            couponCode = couponCode.ToUpperInvariant();
            HttpContext.Session.SetString("AppliedCouponCode", couponCode);
            
            // Redirect to Index so the ajax-cart-form interceptor replaces the HTML
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult RemoveCoupon()
        {
            HttpContext.Session.Remove("AppliedCouponCode");
            return RedirectToAction(nameof(Index));
        }
    }
}
