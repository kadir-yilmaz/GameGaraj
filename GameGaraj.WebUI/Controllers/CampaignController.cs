using GameGaraj.WebUI.Models.Campaigns;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers
{
    public class CampaignController : Controller
    {
        private readonly ICampaignService _campaignService;
        private readonly ICatalogService _catalogService;
        private readonly ILogger<CampaignController> _logger;

        public CampaignController(
            ICampaignService campaignService, 
            ICatalogService catalogService,
            ILogger<CampaignController> logger)
        {
            _campaignService = campaignService;
            _catalogService = catalogService;
            _logger = logger;
        }

        /// <summary>
        /// Tüm aktif kampanyaları listeler
        /// Route: /Campaign
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var rules = await _campaignService.GetAllRulesAsync();
                var now = DateTime.UtcNow;
                var activeRules = rules
                    .Where(r => r.IsActive
                                && (!r.StartDate.HasValue || r.StartDate.Value <= now)
                                && (!r.EndDate.HasValue || r.EndDate.Value.Date >= now.Date))
                    .ToList();

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

                ViewBag.RuleProducts = ruleProducts;
                return View(activeRules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignController] Index hatası");
                return View(new List<CampaignRuleViewModel>());
            }
        }

        /// <summary>
        /// Belirli bir kampanyanın detaylarını gösterir
        /// Route: /Campaign/Detail/{id}
        /// </summary>
        public async Task<IActionResult> Detail(int id)
        {
            try
            {
                var rule = await _campaignService.GetRuleByIdAsync(id);
                var now = DateTime.UtcNow;
                if (rule == null
                    || !rule.IsActive
                    || (rule.StartDate.HasValue && rule.StartDate.Value > now)
                    || (rule.EndDate.HasValue && rule.EndDate.Value.Date < now.Date))
                {
                    return NotFound("Kampanya bulunamadı veya aktif değil.");
                }

                if (!string.IsNullOrEmpty(rule.ProductId))
                {
                    var product = await _catalogService.GetProductByIdAsync(rule.ProductId);
                    ViewBag.Product = product;
                }

                if (!string.IsNullOrEmpty(rule.CategoryId))
                {
                    var categories = await _catalogService.GetAllCategoriesAsync();
                    var category = FindCategoryRecursive(categories, rule.CategoryId);
                    ViewBag.CategoryName = category?.Name ?? "Seçili Kategori";
                }

                return View(rule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignController] Detail hatası — ID: {Id}", id);
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Tüm herkese açık kuponları listeler
        /// Route: /Campaign/Coupons
        /// </summary>
        public async Task<IActionResult> Coupons()
        {
            try
            {
                var coupons = await _campaignService.GetPublicCouponsAsync();
                var activeCoupons = coupons.Where(c => !c.IsUsed && (!c.ExpiryDate.HasValue || c.ExpiryDate.Value >= DateTime.Now)).ToList();
                return View(activeCoupons);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignController] Coupons hatası");
                return View(new List<CouponViewModel>());
            }
        }

        /// <summary>
        /// Tüm ödül (kupon kazanma) kurallarını listeler
        /// Route: /Campaign/Rewards
        /// </summary>
        public async Task<IActionResult> Rewards()
        {
            try
            {
                var rewards = await _campaignService.GetAllRewardRulesAsync();
                var activeRewards = rewards.Where(r => r.IsActive).ToList();
                return View(activeRewards);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignController] Rewards hatası");
                return View(new List<CouponRewardRuleViewModel>());
            }
        }

        /// <summary>
        /// Belirli bir kuponun detaylarını gösterir
        /// Route: /Campaign/CouponDetail/{id}
        /// </summary>
        public async Task<IActionResult> CouponDetail(int id)
        {
            try
            {
                var coupon = await _campaignService.GetCouponByIdAsync(id);
                if (coupon == null || coupon.IsUsed || (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < DateTime.Now))
                {
                    return NotFound("Kupon bulunamadı veya geçerlilik süresi dolmuş.");
                }

                return View(coupon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignController] CouponDetail hatası — ID: {Id}", id);
                return RedirectToAction(nameof(Coupons));
            }
        }

        /// <summary>
        /// Belirli bir kupon kazanma kuralının detaylarını gösterir
        /// Route: /Campaign/RewardDetail/{id}
        /// </summary>
        public async Task<IActionResult> RewardDetail(int id)
        {
            try
            {
                var reward = await _campaignService.GetRewardRuleByIdAsync(id);
                if (reward == null || !reward.IsActive)
                {
                    return NotFound("Kupon kazanma kuralı bulunamadı veya aktif değil.");
                }

                return View(reward);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignController] RewardDetail hatası — ID: {Id}", id);
                return RedirectToAction(nameof(Rewards));
            }
        }

        private GameGaraj.WebUI.Models.Products.CategoryViewModel? FindCategoryRecursive(List<GameGaraj.WebUI.Models.Products.CategoryViewModel> categories, string categoryId)
        {
            foreach (var cat in categories)
            {
                if (cat.Id == categoryId) return cat;
                if (cat.Children != null && cat.Children.Any())
                {
                    var found = FindCategoryRecursive(cat.Children, categoryId);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
