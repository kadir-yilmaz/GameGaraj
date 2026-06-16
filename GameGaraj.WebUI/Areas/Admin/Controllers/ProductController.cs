using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin, editor")]
    public class ProductController : Controller
    {
        private readonly ICatalogService _catalogService;
        private readonly IPhotoStockService _photoStockService;
        private readonly ILogger<ProductController> _logger;

        public ProductController(ICatalogService catalogService, IPhotoStockService photoStockService, ILogger<ProductController> logger)
        {
            _catalogService = catalogService;
            _photoStockService = photoStockService;
            _logger = logger;
        }

        public async Task<IActionResult> Index([FromQuery] ProductAdminIndexViewModel filter)
        {
            var roots = await _catalogService.GetAllCategoriesAsync();
            var flattenedList = new List<CategoryDropdownViewModel>();
            FlattenCategories(roots, flattenedList, "");

            filter.CategoryOptions = flattenedList
                .Select(category => new SelectListItem(category.DisplayName, category.Id))
                .Prepend(new SelectListItem("Tum kategoriler", string.Empty))
                .ToList();

            filter.Results = await _catalogService.GetAdminProductsPageAsync(
                filter.Query,
                filter.CategoryId,
                filter.IsFeatured,
                filter.IsActive,
                filter.StockState,
                filter.Page,
                filter.PageSize);

            return View(filter);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var roots = await _catalogService.GetAllCategoriesAsync();
            var flattenedList = new List<CategoryDropdownViewModel>();
            FlattenCategories(roots, flattenedList, "");

            ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductCreateInput model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        Console.WriteLine($"[ProductCreateValidation] Error: {error.ErrorMessage}, Exception: {error.Exception?.Message}");
                    }
                }
                var roots = await _catalogService.GetAllCategoriesAsync();
                var flattenedList = new List<CategoryDropdownViewModel>();
                FlattenCategories(roots, flattenedList, "");

                ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName");
                return View(model);
            }

            // Image Upload
            if (model.Photos != null && model.Photos.Count > 0)
            {
                List<string> uploadedUrls;
                try
                {
                    uploadedUrls = await _photoStockService.UploadPhotosAsync(model.Photos, model.Brand, model.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ProductCreate] Photo upload failed");
                    ModelState.AddModelError("", "Fotoğraf yüklenirken bir hata oluştu.");
                    var roots = await _catalogService.GetAllCategoriesAsync();
                    var flattenedList = new List<CategoryDropdownViewModel>();
                    FlattenCategories(roots, flattenedList, "");

                    ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName");
                    return View(model);
                }
                if (uploadedUrls.Any())
                {
                    if (model.CoverImageKey.StartsWith("new:", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(model.CoverImageKey.Substring("new:".Length), out var coverIndex)
                        && coverIndex >= 0
                        && coverIndex < uploadedUrls.Count)
                    {
                        var coverUrl = uploadedUrls[coverIndex];
                        uploadedUrls.RemoveAt(coverIndex);
                        uploadedUrls.Insert(0, coverUrl);
                    }
                    // PhotoStock API "/photos/filename.jpg" dönüyor ama BaseAddress ServiceExtension'da ayarlı.
                    // ImageUrls db'ye kaydederken tam endpoint yazmıyoruz, Catalog API'si/UI tarafında PhotoStockUrl ile merge edilecek
                    model.ImageUrls = uploadedUrls;
                }
            }

            var createdProduct = await _catalogService.CreateProductAsync(model);

            if (createdProduct == null)
            {
                ModelState.AddModelError("", "Ürün oluşturulurken bir hata oluştu.");
                var roots = await _catalogService.GetAllCategoriesAsync();
                var flattenedList = new List<CategoryDropdownViewModel>();
                FlattenCategories(roots, flattenedList, "");

                ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName");
                return View(model);
            }

            return RedirectToAction(nameof(Index), "Product", new { area = "Admin" });
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

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var product = await _catalogService.GetProductByIdAsync(id);
            if (product == null)
            {
                TempData["Error"] = "Ürün bulunamadı.";
                return RedirectToAction(nameof(Index), "Product", new { area = "Admin" });
            }

            var roots = await _catalogService.GetAllCategoriesAsync();
            var flattenedList = new List<CategoryDropdownViewModel>();
            FlattenCategories(roots, flattenedList, "");
            ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName", product.CategoryId);

            var model = new ProductUpdateInput
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Brand = product.Brand,
                Slug = product.Slug,
                Price = product.Price,
                Stock = product.Stock,
                IsActive = product.IsActive,
                IsFeatured = product.IsFeatured,
                CategoryId = product.CategoryId,
                ImageUrls = product.ImageUrls,
                Specs = product.Specs
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ProductUpdateInput model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        Console.WriteLine($"[ProductEditValidation] Error: {error.ErrorMessage}, Exception: {error.Exception?.Message}");
                    }
                }
                var roots = await _catalogService.GetAllCategoriesAsync();
                var flattenedList = new List<CategoryDropdownViewModel>();
                FlattenCategories(roots, flattenedList, "");
                ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName", model.CategoryId);
                return View(model);
            }

            var uploadedUrlsForCover = new List<string>();

            // Image Upload for Edit
            if (model.Photos != null && model.Photos.Count > 0)
            {
                // Mevcut fotoğraflara yenilerini ekle (Max 5 kontrolü)
                var currentCount = model.ImageUrls?.Count ?? 0;
                var spaceLeft = 5 - currentCount;

                if (spaceLeft > 0)
                {
                    // Eğer yer kalmışsa, en fazla kalan yer kadar fotoğraf al (ya da IPhotoStockService içinden hata döner)
                    List<string> uploadedUrls;
                    try
                    {
                        uploadedUrls = await _photoStockService.UploadPhotosAsync(model.Photos, model.Brand, model.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ProductEdit] Photo upload failed for product {ProductId}", model.Id);
                        ModelState.AddModelError("", "Fotoğraf yüklenirken bir hata oluştu.");
                        var roots = await _catalogService.GetAllCategoriesAsync();
                        var flattenedList = new List<CategoryDropdownViewModel>();
                        FlattenCategories(roots, flattenedList, "");
                        ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName", model.CategoryId);
                        return View(model);
                    }

                    if (uploadedUrls.Any())
                    {
                        if (model.ImageUrls == null) model.ImageUrls = new List<string>();
                        uploadedUrlsForCover = uploadedUrls.Take(spaceLeft).ToList();
                        model.ImageUrls.AddRange(uploadedUrlsForCover);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Bir ürüne en fazla 5 adet fotoğraf yüklenebilir. Lütfen önce mevcut fotoğraflardan bazılarını silin.");
                    var roots = await _catalogService.GetAllCategoriesAsync();
                    var flattenedList = new List<CategoryDropdownViewModel>();
                    FlattenCategories(roots, flattenedList, "");
                    ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName", model.CategoryId);
                    return View(model);
                }
            }

            if (!string.IsNullOrWhiteSpace(model.CoverImageKey) && model.ImageUrls != null && model.ImageUrls.Any())
            {
                string? coverImageUrl = null;

                if (model.CoverImageKey.StartsWith("existing:", StringComparison.OrdinalIgnoreCase))
                {
                    coverImageUrl = model.CoverImageKey.Substring("existing:".Length);
                }
                else if (model.CoverImageKey.StartsWith("new:", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(model.CoverImageKey.Substring("new:".Length), out var newImageIndex)
                    && newImageIndex >= 0
                    && newImageIndex < uploadedUrlsForCover.Count)
                {
                    coverImageUrl = uploadedUrlsForCover[newImageIndex];
                }

                if (!string.IsNullOrWhiteSpace(coverImageUrl) && model.ImageUrls.Contains(coverImageUrl))
                {
                    model.ImageUrls.Remove(coverImageUrl);
                    model.ImageUrls.Insert(0, coverImageUrl);
                }
            }

            var result = await _catalogService.UpdateProductAsync(model);
            if (!result)
            {
                TempData["Error"] = "Ürün güncellenirken bir hata oluştu.";
                var roots = await _catalogService.GetAllCategoriesAsync();
                var flattenedList = new List<CategoryDropdownViewModel>();
                FlattenCategories(roots, flattenedList, "");
                ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName", model.CategoryId);
                return View(model);
            }

            TempData["Success"] = "Ürün başarıyla güncellendi.";
            return RedirectToAction(nameof(Index), "Product", new { area = "Admin" });
        }
        public async Task<IActionResult> GetCategoryAttributes(string id)
        {
            var category = await _catalogService.GetCategoryByIdAsync(id);
            if (category == null || category.Attributes == null)
            {
                return Json(new List<CategoryAttributeViewModel>());
            }
            return Json(category.Attributes);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id, string? returnUrl = null)
        {
            var result = await _catalogService.DeleteProductAsync(id);
            if (result)
            {
                TempData["Success"] = "Ürün başarıyla silindi.";
            }
            else
            {
                TempData["Error"] = "Ürün silinirken bir hata oluştu.";
            }
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index), "Product", new { area = "Admin" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFeatured(string id, bool isFeatured)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { success = false, message = "Ürün id gerekli." });
            }

            var product = await _catalogService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound(new { success = false, message = "Ürün bulunamadı." });
            }

            var model = new ProductUpdateInput
            {
                Id = product.Id,
                Name = product.Name,
                Brand = product.Brand,
                Slug = product.Slug,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                IsActive = product.IsActive,
                IsFeatured = isFeatured,
                CategoryId = product.CategoryId,
                ImageUrls = product.ImageUrls,
                Specs = product.Specs
            };

            var result = await _catalogService.UpdateProductAsync(model);
            return Json(new { success = result, isFeatured });
        }
    }
}
