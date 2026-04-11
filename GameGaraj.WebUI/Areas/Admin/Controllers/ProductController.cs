using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Services.Abstract;
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

        public ProductController(ICatalogService catalogService, IPhotoStockService photoStockService)
        {
            _catalogService = catalogService;
            _photoStockService = photoStockService;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _catalogService.GetAllProductsAsync();
            return View(products);
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
                var roots = await _catalogService.GetAllCategoriesAsync();
                var flattenedList = new List<CategoryDropdownViewModel>();
                FlattenCategories(roots, flattenedList, "");

                ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName");
                return View(model);
            }

            // Image Upload
            if (model.Photos != null && model.Photos.Count > 0)
            {
                var uploadedUrls = await _photoStockService.UploadPhotosAsync(model.Photos);
                if (uploadedUrls.Any())
                {
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

            return RedirectToAction(nameof(Index));
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
                return RedirectToAction(nameof(Index));
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
                var roots = await _catalogService.GetAllCategoriesAsync();
                var flattenedList = new List<CategoryDropdownViewModel>();
                FlattenCategories(roots, flattenedList, "");
                ViewBag.Categories = new SelectList(flattenedList, "Id", "DisplayName", model.CategoryId);
                return View(model);
            }

            // Image Upload for Edit
            if (model.Photos != null && model.Photos.Count > 0)
            {
                // Mevcut fotoğraflara yenilerini ekle (Max 5 kontrolü)
                var currentCount = model.ImageUrls?.Count ?? 0;
                var spaceLeft = 5 - currentCount;

                if (spaceLeft > 0)
                {
                    // Eğer yer kalmışsa, en fazla kalan yer kadar fotoğraf al (ya da IPhotoStockService içinden hata döner)
                    var uploadedUrls = await _photoStockService.UploadPhotosAsync(model.Photos);
                    if (uploadedUrls.Any())
                    {
                        if (model.ImageUrls == null) model.ImageUrls = new List<string>();
                        model.ImageUrls.AddRange(uploadedUrls.Take(spaceLeft));
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
            return RedirectToAction(nameof(Index));
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
        public async Task<IActionResult> Delete(string id)
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
            return RedirectToAction(nameof(Index));
        }
    }
}
