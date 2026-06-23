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
                if (uploadedUrls != null && uploadedUrls.Any())
                {
                    model.ImageUrls = CleanImageUrls(uploadedUrls);
                }
                else
                {
                    ModelState.AddModelError("", "Seçilen fotoğraflar yüklenemedi. Depolama servisi (MinIO) veya PhotoStock API bağlantı hatası oluşmuş olabilir.");
                    var roots = await _catalogService.GetAllCategoriesAsync();
                    var flattenedList = new List<CategoryDropdownViewModel>();
                    FlattenCategories(roots, flattenedList, "");

                    ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName");
                    return View(model);
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

        private List<string> CleanImageUrls(List<string>? urls)
        {
            if (urls == null) return new List<string>();
            return urls.Select(url =>
            {
                if (string.IsNullOrWhiteSpace(url)) return string.Empty;
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var uri = new Uri(url);
                        var path = uri.AbsolutePath.TrimStart('/');
                        if (path.Contains("photos/"))
                        {
                            return path.Substring(path.IndexOf("photos/"));
                        }
                        return path;
                    }
                    catch
                    {
                        return url;
                    }
                }
                return url;
            }).Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
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

            List<string> uploadedUrls = null;

            // Image Upload for Edit
            if (model.Photos != null && model.Photos.Count > 0)
            {
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

                if (uploadedUrls == null || !uploadedUrls.Any())
                {
                    ModelState.AddModelError("", "Seçilen fotoğraflar yüklenemedi. Depolama servisi (MinIO) veya PhotoStock API bağlantı hatası oluşmuş olabilir.");
                    var roots = await _catalogService.GetAllCategoriesAsync();
                    var flattenedList = new List<CategoryDropdownViewModel>();
                    FlattenCategories(roots, flattenedList, "");
                    ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName", model.CategoryId);
                    return View(model);
                }
            }

            // Reconstruct and clean the final ImageUrls list based on ImageOrder
            var finalUrls = new List<string>();
            var newPhotoIndex = 0;

            if (model.ImageOrder != null && model.ImageOrder.Any())
            {
                foreach (var item in model.ImageOrder)
                {
                    if (item.StartsWith("existing:", StringComparison.OrdinalIgnoreCase))
                    {
                        var url = item.Substring("existing:".Length);
                        finalUrls.Add(url);
                    }
                    else if (item.StartsWith("new:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (uploadedUrls != null && newPhotoIndex < uploadedUrls.Count)
                        {
                            finalUrls.Add(uploadedUrls[newPhotoIndex]);
                            newPhotoIndex++;
                        }
                    }
                }
            }
            else
            {
                // Fallback: merge existing and new URLs
                if (model.ImageUrls != null)
                {
                    finalUrls.AddRange(model.ImageUrls);
                }
                if (uploadedUrls != null)
                {
                    finalUrls.AddRange(uploadedUrls);
                }
            }

            model.ImageUrls = CleanImageUrls(finalUrls);

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
        [ValidateAntiForgeryToken]
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
