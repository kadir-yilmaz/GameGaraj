using GameGaraj.WebUI.Models.Campaigns;
using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin, editor")]
    public class CampaignController : Controller
    {
        private readonly ICampaignService _campaignService;
        private readonly ICatalogService _catalogService;

        public CampaignController(ICampaignService campaignService, ICatalogService catalogService)
        {
            _campaignService = campaignService;
            _catalogService = catalogService;
        }

        /// <summary>
        /// Tüm indirim kurallarını listeler
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var rules = await _campaignService.GetAllRulesAsync();

            // Kategori adlarını çözmek için kategorileri getir
            var categories = await _catalogService.GetAllCategoriesAsync();
            var flatCategories = new List<CategoryDropdownViewModel>();
            FlattenCategories(categories, flatCategories, "");
            ViewBag.CategoryMap = flatCategories.ToDictionary(c => c.Id, c => c.DisplayName);

            // Ürün adlarını çözmek için (eğer ProductId set edilmişse)
            foreach (var rule in rules)
            {
                if (!string.IsNullOrEmpty(rule.ProductId))
                {
                    var product = await _catalogService.GetProductByIdAsync(rule.ProductId);
                    rule.ProductName = product?.Name ?? "Bilinmeyen Ürün";
                }
            }

            return View(rules);
        }

        /// <summary>
        /// Yeni indirim kuralı oluşturma formu
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Create(string? productId, string? categoryId)
        {
            await LoadCategoriesViewBag(categoryId);
            LoadRuleTypesViewBag();

            var model = new CampaignRuleCreateInput
            {
                ProductId = productId,
                CategoryId = categoryId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7)
            };

            if (!string.IsNullOrEmpty(productId))
            {
                var product = await _catalogService.GetProductByIdAsync(productId);
                ViewBag.PreSelectedProductName = product != null ? $"{product.Brand} {product.Name}" : null;
            }

            return View(model);
        }

        /// <summary>
        /// Yeni indirim kuralı kaydetme
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create(CampaignRuleCreateInput model)
        {
            if (!ModelState.IsValid)
            {
                await LoadCategoriesViewBag();
                LoadRuleTypesViewBag();
                return View(model);
            }

            // Boş string'leri null'a çevir
            if (string.IsNullOrWhiteSpace(model.CategoryId)) model.CategoryId = null;
            if (string.IsNullOrWhiteSpace(model.ProductId)) model.ProductId = null;
            if (string.IsNullOrWhiteSpace(model.Description)) model.Description = null;
            if (string.IsNullOrWhiteSpace(model.BrandName)) model.BrandName = null;
            if (string.IsNullOrWhiteSpace(model.ImageUrl)) model.ImageUrl = null;

            // TotalAmount kuralı için hedef seçim gereksiz — tüm sepete uygulanır
            if (model.RuleType == "TotalAmount")
            {
                model.CategoryId = null;
                model.ProductId = null;
                model.BrandName = null;
            }

            var result = await _campaignService.CreateRuleAsync(model);
            if (!result)
            {
                TempData["Error"] = "İndirim kuralı oluşturulurken bir hata oluştu.";
                await LoadCategoriesViewBag();
                LoadRuleTypesViewBag();
                return View(model);
            }

            TempData["Success"] = "İndirim kuralı başarıyla oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// İndirim kuralı düzenleme formu
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var rule = await _campaignService.GetRuleByIdAsync(id);
            if (rule == null)
            {
                TempData["Error"] = "İndirim kuralı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var model = new CampaignRuleUpdateInput
            {
                Id = rule.Id,
                Name = rule.Name,
                Description = rule.Description,
                RuleType = rule.RuleType,
                CategoryId = rule.CategoryId,
                MinAmount = rule.MinAmount,
                MinQuantity = rule.MinQuantity,
                FreeQuantity = rule.FreeQuantity,
                DiscountRate = rule.DiscountRate,
                IsActive = rule.IsActive,
                ProductId = rule.ProductId,
                BrandName = rule.BrandName,
                FixedDiscount = rule.FixedDiscount,
                StartDate = rule.StartDate,
                EndDate = rule.EndDate,
                ImageUrl = rule.ImageUrl
            };

            // Ürün bazlı ise seçili ürün adını da gönder
            if (!string.IsNullOrEmpty(rule.ProductId))
            {
                var product = await _catalogService.GetProductByIdAsync(rule.ProductId);
                ViewBag.SelectedProductName = product != null
                    ? $"{product.Brand} {product.Name}"
                    : "Bilinmeyen Ürün";
            }

            await LoadCategoriesViewBag(rule.CategoryId);
            LoadRuleTypesViewBag(rule.RuleType);
            return View(model);
        }

        /// <summary>
        /// İndirim kuralı güncelleme
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Edit(CampaignRuleUpdateInput model)
        {
            if (!ModelState.IsValid)
            {
                await LoadCategoriesViewBag(model.CategoryId);
                LoadRuleTypesViewBag(model.RuleType);
                return View(model);
            }

            if (model.RuleType == "TotalAmount")
            {
                model.CategoryId = null;
                model.ProductId = null;
                model.BrandName = null;
            }

            var result = await _campaignService.UpdateRuleAsync(model);
            if (!result)
            {
                TempData["Error"] = "İndirim kuralı güncellenirken bir hata oluştu.";
                await LoadCategoriesViewBag(model.CategoryId);
                LoadRuleTypesViewBag(model.RuleType);
                return View(model);
            }

            TempData["Success"] = "İndirim kuralı başarıyla güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// İndirim kuralı silme
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _campaignService.DeleteRuleAsync(id);
            if (result)
            {
                TempData["Success"] = "İndirim kuralı başarıyla silindi.";
            }
            else
            {
                TempData["Error"] = "İndirim kuralı silinirken bir hata oluştu.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ───── Product Search (AJAX) ─────

        /// <summary>
        /// Ürün adına göre arama yapar — kampanya formu için AJAX endpoint.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchProducts(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Json(new List<object>());

            var products = await _catalogService.SearchProductsAsync(q);
            if (!products.Any())
            {
                var fallbackPage = await _catalogService.GetAdminProductsPageAsync(query: q, isActive: true, page: 1, pageSize: 20);
                products = fallbackPage.Items;
            }

            var result = products.Take(10).Select(p => new
            {
                id = p.Id,
                name = $"{p.Brand} {p.Name}",
                price = p.Price.ToString("C2")
            });
            return Json(result);
        }

        // ───── Helpers ─────

        private async Task LoadCategoriesViewBag(string? selectedId = null)
        {
            var roots = await _catalogService.GetAllCategoriesAsync();
            var flattenedList = new List<CategoryDropdownViewModel>();
            FlattenCategories(roots, flattenedList, "");
            ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName", selectedId);
        }

        private void LoadRuleTypesViewBag(string? selected = null)
        {
            var ruleTypes = new List<SelectListItem>
            {
                new("Toplam Tutar İndirimi", "TotalAmount"),
                new("X Al Y Bedava", "BuyXGetYFree"),
                new("En Ucuz Ürüne İndirim", "CheapestItemDiscount"),
                new("Seçili Ürün/Marka/Kategori İndirimi", "BrandDiscount")
            };
            ViewBag.RuleTypes = new SelectList(ruleTypes, "Value", "Text", selected);
        }

        private void FlattenCategories(List<CategoryViewModel> categories, List<CategoryDropdownViewModel> result, string prefix)
        {
            foreach (var category in categories)
            {
                result.Add(new CategoryDropdownViewModel { Id = category.Id, DisplayName = prefix + category.Name });
                if (category.Children != null && category.Children.Any())
                {
                    FlattenCategories(category.Children, result, prefix + category.Name + " > ");
                }
            }
        }
    }
}
