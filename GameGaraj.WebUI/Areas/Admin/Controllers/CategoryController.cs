using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin, editor")]
    public class CategoryController : Controller
    {
        private readonly ICatalogService _catalogService;

        public CategoryController(ICatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _catalogService.GetAllCategoriesAsync();
            
            // Map ParentNames
            foreach (var category in categories)
            {
                if (!string.IsNullOrEmpty(category.ParentId))
                {
                    category.ParentName = categories.FirstOrDefault(c => c.Id == category.ParentId)?.Name;
                }
            }

            return View(categories);
        }

        [HttpGet]
        public async Task<IActionResult> Create(string? parentId = null)
        {
            var roots = await _catalogService.GetAllCategoriesAsync();
            var flattenedList = new List<CategoryDropdownViewModel>();
            FlattenCategories(roots, flattenedList, "");

            ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName", parentId);
            
            var model = new CategoryCreateInput { ParentId = parentId };
            return View(model);
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

        [HttpPost]
        public async Task<IActionResult> Create(CategoryCreateInput model, List<CategoryAttributeInput> attributes)
        {
            if (!ModelState.IsValid)
            {
                var roots = await _catalogService.GetAllCategoriesAsync();
                var flattenedList = new List<CategoryDropdownViewModel>();
                FlattenCategories(roots, flattenedList, "");
                ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName");
                return View(model);
            }

            // Sanitize ParentId (Empty string to null)
            if (string.IsNullOrEmpty(model.ParentId)) model.ParentId = null;

            // 1. Kategoriyi oluştur
            var createdCategory = await _catalogService.CreateCategoryAsync(model);

            if (createdCategory == null)
            {
                TempData["Error"] = "Kategori oluşturulurken API katmanında bir hata oluştu. Lütfen bağlantıları kontrol edin.";
                ModelState.AddModelError("", "Kategori oluşturulamadı.");
                
                var roots = await _catalogService.GetAllCategoriesAsync();
                var flattenedList = new List<CategoryDropdownViewModel>();
                FlattenCategories(roots, flattenedList, "");
                ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName");
                return View(model);
            }

            // 2. Varsa özellikleri (attributes) ekle
            if (attributes != null && attributes.Any())
            {
                foreach (var attribute in attributes)
                {
                    if (string.IsNullOrWhiteSpace(attribute.Name)) continue;
                    NormalizeAttribute(attribute);
                    await _catalogService.AddAttributeAsync(createdCategory.Id, attribute);
                }
            }

            TempData["Success"] = "Kategori başarıyla oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var category = await _catalogService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                TempData["Error"] = "Kategori bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var roots = await _catalogService.GetAllCategoriesAsync();
            var flattenedList = new List<CategoryDropdownViewModel>();
            FlattenCategories(roots, flattenedList, "");

            // Mevcut kategoriyi ve onun alt dallarını listeden çıkar (kendi kendine ebeveyn olmasın)
            var filteredList = flattenedList.Where(c => c.Id != id).ToList();

            ViewBag.Categories = new SelectList(filteredList, "Id", "DisplayName");

            var model = new CategoryCreateInput
            {
                Name = category.Name,
                ParentId = category.ParentId
            };

            ViewBag.ExistingAttributes = category.Attributes;

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(string id, CategoryCreateInput model, List<CategoryAttributeInput> attributes)
        {
            if (!ModelState.IsValid)
            {
                var roots = await _catalogService.GetAllCategoriesAsync();
                var flattenedList = new List<CategoryDropdownViewModel>();
                FlattenCategories(roots, flattenedList, "");
                var filteredList = flattenedList.Where(c => c.Id != id).ToList();
                ViewBag.Categories = new SelectList(filteredList, "Id", "DisplayName");
                
                // Re-fetch existing attributes if validation fails to re-render the form correctly
                var category = await _catalogService.GetCategoryByIdAsync(id);
                if (category != null)
                {
                    ViewBag.ExistingAttributes = category.Attributes;
                }
                return View(model);
            }

            if (string.IsNullOrEmpty(model.ParentId)) model.ParentId = null;

            var result = await _catalogService.UpdateCategoryAsync(id, model);
            if (result == null)
            {
                TempData["Error"] = "Kategori güncellenirken bir hata oluştu.";
                
                var roots = await _catalogService.GetAllCategoriesAsync();
                var flattenedList = new List<CategoryDropdownViewModel>();
                FlattenCategories(roots, flattenedList, "");
                var filteredList = flattenedList.Where(c => c.Id != id).ToList();
                ViewBag.Categories = new SelectList(filteredList, "Id", "DisplayName");

                // Re-fetch existing attributes if update fails
                var category = await _catalogService.GetCategoryByIdAsync(id);
                if (category != null)
                {
                    ViewBag.ExistingAttributes = category.Attributes;
                }
                
                return View(model);
            }

            // Yeni eklenen özellikler varsa onları kaydet
            if (attributes != null && attributes.Any())
            {
                foreach (var attr in attributes)
                {
                    if (string.IsNullOrWhiteSpace(attr.Name)) continue;
                    NormalizeAttribute(attr);
                    await _catalogService.AddAttributeAsync(id, attr);
                }
            }

            TempData["Success"] = "Kategori başarıyla güncellendi.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAttribute(string categoryId, string attributeId)
        {
            var result = await _catalogService.DeleteAttributeAsync(categoryId, attributeId);
            if (result)
            {
                TempData["Success"] = "Özellik başarıyla silindi.";
            }
            else
            {
                TempData["Error"] = "Özellik silinirken bir hata oluştu.";
            }

            return RedirectToAction(nameof(Edit), new { id = categoryId });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAttribute(string categoryId, string attributeId, CategoryAttributeInput model)
        {
            NormalizeAttribute(model);
            var result = await _catalogService.UpdateAttributeAsync(categoryId, attributeId, model);
            if (result)
            {
                TempData["Success"] = "Özellik başarıyla güncellendi.";
            }
            else
            {
                TempData["Error"] = "Özellik güncellenirken bir hata oluştu.";
            }

            return RedirectToAction(nameof(Edit), new { id = categoryId });
        }

        private static void NormalizeAttribute(CategoryAttributeInput attribute)
        {
            attribute.Name = attribute.Name?.Trim() ?? string.Empty;
            attribute.DisplayName = attribute.DisplayName?.Trim() ?? string.Empty;
            attribute.Type = string.IsNullOrWhiteSpace(attribute.Type) ? "Text" : attribute.Type.Trim();

            if (!string.Equals(attribute.Type, "Dropdown", StringComparison.OrdinalIgnoreCase))
            {
                attribute.Options = null;
                return;
            }

            attribute.Options = attribute.Options?
                .Select(option => option?.Trim())
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();
        }
    }
}
