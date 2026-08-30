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
        private readonly IReviewService _reviewService;

        public HomeController(ILogger<HomeController> logger, 
            ICatalogService catalogService, 
            IBasketService basketService, 
            IFavoritesService favoritesService,
            ICampaignService campaignService,
            IReviewService reviewService)
        {
            _logger = logger;
            _catalogService = catalogService;
            _basketService = basketService;
            _favoritesService = favoritesService;
            _campaignService = campaignService;
            _reviewService = reviewService;
        }

        public async Task<IActionResult> Index()
        {
            var featuredTask = _catalogService.GetFeaturedProductsAsync();
            var categoriesTask = _catalogService.GetAllCategoriesAsync();
            var rulesTask = _campaignService.GetAllRulesAsync();
            var couponsTask = _campaignService.GetPublicCouponsAsync();
            var rewardRulesTask = _campaignService.GetAllRewardRulesAsync();
            var carouselTask = _campaignService.GetCarouselImagesAsync();

            await Task.WhenAll(featuredTask, categoriesTask, rulesTask, couponsTask, rewardRulesTask, carouselTask);

            var featuredProducts = featuredTask.Result ?? new List<GameGaraj.WebUI.Models.Products.ProductViewModel>();
            var allCategories = categoriesTask.Result ?? new List<GameGaraj.WebUI.Models.Products.CategoryViewModel>();

            var userStateTask = ApplyUserProductStateAsync(featuredProducts);
            var reviewSummaryTask = ApplyReviewSummariesAsync(featuredProducts);

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
                var rules = rulesTask.Result ?? new List<GameGaraj.WebUI.Models.Campaigns.CampaignRuleViewModel>();
                var coupons = couponsTask.Result ?? new List<GameGaraj.WebUI.Models.Campaigns.CouponViewModel>();
                var rewardRules = rewardRulesTask.Result ?? new List<GameGaraj.WebUI.Models.Campaigns.CouponRewardRuleViewModel>();
                var carouselList = carouselTask.Result ?? new List<GameGaraj.WebUI.Models.Campaigns.CarouselImageViewModel>();

                ViewBag.CarouselImages = carouselList.Select(img => img.ImageUrl).ToList();

                var now = DateTime.UtcNow;
                var activeRules = rules
                    .Where(r => r.IsActive
                                && (!r.StartDate.HasValue || r.StartDate.Value <= now)
                                && (!r.EndDate.HasValue || r.EndDate.Value.Date >= now.Date))
                    .ToList();

                var uniqueProductIds = activeRules
                    .Select(r => r.ProductId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .ToList();

                var ruleProductTasks = uniqueProductIds.Select(async id => new
                {
                    Id = id,
                    Product = await _catalogService.GetProductByIdAsync(id!)
                });

                var ruleProductResults = await Task.WhenAll(ruleProductTasks);
                var ruleProducts = ruleProductResults
                    .Where(x => x.Product != null)
                    .ToDictionary(x => x.Id!, x => x.Product!);

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

            await Task.WhenAll(userStateTask, reviewSummaryTask);

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
            await ApplyReviewSummariesAsync(products);

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

        private async Task ApplyReviewSummariesAsync(List<GameGaraj.WebUI.Models.Products.ProductViewModel> products)
        {
            if (products == null || products.Count == 0)
            {
                return;
            }

            var summaries = await _reviewService.GetProductReviewSummariesAsync(products.Select(product => product.Id));
            foreach (var product in products)
            {
                if (summaries.TryGetValue(product.Id, out var summary))
                {
                    product.AverageRating = summary.AverageRating;
                    product.ReviewCount = summary.TotalCount;
                }
                else
                {
                    product.AverageRating = 0;
                    product.ReviewCount = 0;
                }
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
