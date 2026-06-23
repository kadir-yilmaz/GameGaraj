using GameGaraj.WebUI.Models;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GameGaraj.WebUI.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ICatalogService _catalogService;
        private readonly IBasketService _basketService;
        private readonly IFavoritesService _favoritesService;
        private readonly ICampaignService _campaignService;

        public HomeController(ILogger<HomeController> logger, 
            ICatalogService catalogService, 
            IBasketService basketService, 
            IFavoritesService favoritesService,
            ICampaignService campaignService)
        {
            _logger = logger;
            _catalogService = catalogService;
            _basketService = basketService;
            _favoritesService = favoritesService;
            _campaignService = campaignService;
        }

        public async Task<IActionResult> Index()
        {
            var featuredProducts = await _catalogService.GetFeaturedProductsAsync();
            await ApplyUserProductStateAsync(featuredProducts);

            var allCategories = await _catalogService.GetAllCategoriesAsync();
            var flattenedCategories = new List<GameGaraj.WebUI.Models.Products.CategoryViewModel>();
            void Flatten(IEnumerable<GameGaraj.WebUI.Models.Products.CategoryViewModel> categories)
            {
                foreach (var c in categories)
                {
                    flattenedCategories.Add(c);
                    if (c.Children != null && c.Children.Any()) Flatten(c.Children);
                }
            }
            Flatten(allCategories);

            var homeCategories = flattenedCategories.Where(c => c.IsShowOnHome).ToList();
            ViewBag.HomeCategories = homeCategories;

            try
            {
                var rules = await _campaignService.GetAllRulesAsync();
                var coupons = await _campaignService.GetPublicCouponsAsync();
                var rewardRules = await _campaignService.GetAllRewardRulesAsync();
                var carouselList = await _campaignService.GetCarouselImagesAsync();

                ViewBag.CarouselImages = carouselList.Select(img => img.ImageUrl).ToList();

                var activeRules = rules.Where(r => r.IsActive).ToList();
                var ruleProducts = new Dictionary<string, GameGaraj.WebUI.Models.Products.ProductViewModel>();
                foreach (var rule in activeRules)
                {
                    if (!string.IsNullOrEmpty(rule.ProductId) && !ruleProducts.ContainsKey(rule.ProductId))
                    {
                        var product = await _catalogService.GetProductByIdAsync(rule.ProductId);
                        if (product != null)
                        {
                            ruleProducts[rule.ProductId] = product;
                        }
                    }
                }

                ViewBag.ActiveRules = activeRules;
                ViewBag.RuleProducts = ruleProducts;
                ViewBag.PublicCoupons = coupons.Where(c => !c.IsUsed && (!c.ExpiryDate.HasValue || c.ExpiryDate.Value >= DateTime.Now)).ToList();
                ViewBag.RewardRules = rewardRules.Where(rr => rr.IsActive).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HomeController] Fırsat verileri yüklenirken hata oluştu.");
                ViewBag.ActiveRules = new List<GameGaraj.WebUI.Models.Campaigns.CampaignRuleViewModel>();
                ViewBag.PublicCoupons = new List<GameGaraj.WebUI.Models.Campaigns.CouponViewModel>();
                ViewBag.RewardRules = new List<GameGaraj.WebUI.Models.Campaigns.CouponRewardRuleViewModel>();
                ViewBag.CarouselImages = new List<string>();
            }

            return View(featuredProducts);
        }

        [HttpGet]
        public async Task<IActionResult> CategoryShowcase(string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
            {
                return BadRequest();
            }

            var products = (await _catalogService.GetProductsByCategoryAsync(categoryId))
                .Take(5)
                .ToList();

            await ApplyUserProductStateAsync(products);

            ViewBag.ShowFeaturedBadge = false;
            return PartialView("_HomeCategoryProducts", products);
        }

        private async Task ApplyUserProductStateAsync(List<GameGaraj.WebUI.Models.Products.ProductViewModel> products)
        {
            if (products == null || products.Count == 0)
            {
                return;
            }

            var basket = await _basketService.GetBasketAsync();
            var favoriteIds = await _favoritesService.GetFavoriteProductIdsAsync();
            var basketProductIds = basket?.Items?
                .Select(x => x.ProductId?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var product in products)
            {
                product.IsInBasket = basketProductIds.Contains(product.Id?.Trim() ?? string.Empty);
                product.IsFavorite = favoriteIds.Contains(product.Id ?? string.Empty);
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
