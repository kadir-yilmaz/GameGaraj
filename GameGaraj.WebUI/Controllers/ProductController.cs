using AspNetCoreHero.ToastNotification.Abstractions;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Models.Reviews;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers
{
    public class ProductController : Controller
    {
        private readonly ICatalogService _catalogService;
        private readonly IBasketService _basketService;
        private readonly IFavoritesService _favoritesService;
        private readonly IReviewService _reviewService;
        private readonly INotyfService _notyf;

        public ProductController(ICatalogService catalogService, IBasketService basketService, IFavoritesService favoritesService, IReviewService reviewService, INotyfService notyf)
        {
            _catalogService = catalogService;
            _basketService = basketService;
            _favoritesService = favoritesService;
            _reviewService = reviewService;
            _notyf = notyf;
        }

        [Route("product/c/{category}")]
        [Route("product")]
        public async Task<IActionResult> Index(string? category, string? categoryId, string? sortBy, decimal? minPrice, decimal? maxPrice, Dictionary<string, string[]>? specs, string? search, string? brand)
        {
            CategoryViewModel? categoryModel = null;

            // 1. Resolve Category from Slug or ID
            if (!string.IsNullOrEmpty(category))
            {
                categoryModel = await _catalogService.GetCategoryBySlugAsync(category);
                if (categoryModel != null)
                {
                    categoryId = categoryModel.Id;
                }
            }
            else if (!string.IsNullOrEmpty(categoryId))
            {
                categoryModel = await _catalogService.GetCategoryByIdAsync(categoryId);
                
                // SEO Redirect: If we have a slug, redirect to /product/c/{category}
                if (categoryModel != null && !string.IsNullOrEmpty(categoryModel.Slug))
                {
                    return RedirectToActionPermanent("Index", new { category = categoryModel.Slug, sortBy, minPrice, maxPrice, specs, search, brand });
                }
            }

            // ... (rest of the logic remains the same)
            // Fix: remove non-spec query parameters from specs
            if (specs != null)
            {
                var reservedSpecKeys = new[]
                {
                    "category", "categoryId", "CategoryId", "sortBy", "minPrice", "maxPrice", "search", "brand"
                };

                foreach (var key in reservedSpecKeys)
                {
                    specs.Remove(key);
                }
            }

            List<ProductViewModel> products;

            // Keyword search
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                products = await _catalogService.SearchProductsAsync(search);
                
                if (!string.IsNullOrEmpty(categoryId)) products = products.Where(p => p.CategoryId == categoryId).ToList();
                if (minPrice.HasValue) products = products.Where(p => p.Price >= minPrice.Value).ToList();
                if (maxPrice.HasValue) products = products.Where(p => p.Price <= maxPrice.Value).ToList();
                if (!string.IsNullOrWhiteSpace(brand))
                {
                    var normalizedBrand = brand.Trim();
                    products = products.Where(p =>
                        string.Equals(p.Brand?.Trim(), normalizedBrand, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(p.Name) &&
                            p.Name.Trim().StartsWith($"{normalizedBrand} ", StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                }
            }
            else
            {
                products = await _catalogService.GetAllProductsAsync(categoryId, sortBy, minPrice, maxPrice, specs, brand);
            }

            var categories = await _catalogService.GetAllCategoriesAsync();
            var brandSourceProducts = await _catalogService.GetAllProductsAsync(categoryId);

            // Setup ViewBags
            ViewBag.CurrentCategoryName = categoryModel?.Name ?? "Tüm Ürünler";
            ViewBag.CurrentCategoryAttributes = categoryModel?.Attributes;
            ViewBag.CategoryId = categoryId;
            ViewBag.CategorySlug = categoryModel?.Slug;
            ViewBag.Categories = categories;
            ViewBag.SortBy = sortBy;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.SelectedSpecs = specs ?? new Dictionary<string, string[]>();
            ViewBag.Search = search;
            ViewBag.Brand = brand;
            ViewBag.Brands = brandSourceProducts
                .Select(p => p.Brand)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

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
            await ApplyReviewSummariesAsync(products);

            return View(products);
        }

        [Route("product/p/{slug}")]
        [Route("product/detail/{slug}")]
        public async Task<IActionResult> Detail(string slug)
        {
            var product = await _catalogService.GetProductBySlugAsync(slug);

            if (product == null)
            {
                product = await _catalogService.GetProductByIdAsync(slug);
                if (product == null) return NotFound();
                if (!string.IsNullOrEmpty(product.Slug)) return RedirectToActionPermanent("Detail", new { slug = product.Slug });
            }

            if (product == null)
                return NotFound();

            var basket = await _basketService.GetBasketAsync();
            var basketProductIds = basket?.Items?
                .Select(x => x.ProductId?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var productId = product.Id?.Trim() ?? string.Empty;
            product.IsInBasket = basketProductIds.Contains(productId);

            product.IsFavorite = await _favoritesService.IsFavoriteAsync(productId);

            var reviews = await _reviewService.GetProductReviewsAsync(productId, 0, 10);
            ViewBag.Reviews = reviews;

            if (User.Identity?.IsAuthenticated == true)
            {
                ViewBag.CanReview = await _reviewService.CanReviewAsync(productId);
                ViewBag.UserReview = await _reviewService.GetUserReviewAsync(productId);
            }

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReview(CreateReviewInput input, string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("SignIn", "Auth");
            }

            var result = await _reviewService.CreateAsync(input);
            NotifyReviewResult(result);
            return RedirectToLocalOrProduct(input.ProductId, returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReview(UpdateReviewInput input, string? productId = null, string? returnUrl = null)
        {
            var result = await _reviewService.UpdateAsync(input);
            NotifyReviewResult(result);
            return RedirectToLocalOrProduct(productId ?? input.ProductId, returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReview(string reviewId, string productId, string? returnUrl = null)
        {
            var result = await _reviewService.DeleteAsync(reviewId);
            NotifyReviewResult(result);
            return RedirectToLocalOrProduct(productId, returnUrl);
        }

        private void NotifyReviewResult(ReviewMutationResultViewModel result)
        {
            if (result.Succeeded)
            {
                if (result.HasProfanity || result.HasPriceInfo)
                {
                    _notyf.Warning(result.Message);
                    return;
                }

                _notyf.Success(result.Message);
                return;
            }

            _notyf.Error(string.IsNullOrWhiteSpace(result.Message) ? "Islem tamamlanamadi." : result.Message);
        }

        private async Task ApplyReviewSummariesAsync(List<ProductViewModel> products)
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

        private IActionResult RedirectToLocalOrProduct(string productId, string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Detail), new { slug = productId });
        }

        [HttpGet("api/products/search")]
        public async Task<IActionResult> SearchProducts(string q)
        {
            q = q?.Trim() ?? string.Empty;

            if (q.Length < 2)
            {
                return Json(new { categories = new List<object>(), brands = new List<object>(), products = new List<object>() });
            }

            var suggestions = await _catalogService.SearchSuggestionsAsync(q);

            if (!suggestions.Any())
            {
                var fallbackCategories = await _catalogService.SearchCategoriesAsync(q);
                var fallbackBrands = await _catalogService.SearchBrandsAsync(q);
                var fallbackProducts = await _catalogService.SearchProductsAsync(q);

                return Json(new
                {
                    categories = fallbackCategories
                        .Take(3)
                        .Select(c => new
                        {
                            id = c.Id,
                            name = c.Name,
                            url = $"/product/c/{c.Slug}"
                        }),
                    brands = fallbackBrands
                        .Take(3)
                        .Select(b => new
                        {
                            id = b,
                            name = b,
                            url = $"/Product?brand={Uri.EscapeDataString(b)}"
                        }),
                    products = fallbackProducts
                        .Take(10)
                        .Select(p => new
                        {
                            id = p.Id,
                            name = p.Name,
                            price = p.Price.ToString("C2"),
                            imageUrl = p.FirstImageUrl,
                            url = $"/product/p/{p.Slug}"
                        })
                });
            }

            var matchingCategories = suggestions
                .Where(s => string.Equals(s.Type, "category", StringComparison.OrdinalIgnoreCase))
                .GroupBy(s => s.Id)
                .Select(g => g.First())
                .Take(3)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    url = !string.IsNullOrWhiteSpace(c.Slug)
                        ? $"/product/c/{c.Slug}"
                        : $"/Product?categoryId={Uri.EscapeDataString(c.Id)}"
                })
                .ToList();

            var matchingBrands = suggestions
                .Where(s => string.Equals(s.Type, "brand", StringComparison.OrdinalIgnoreCase))
                .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(3)
                .Select(b => new
                {
                    id = b.Id,
                    name = b.Name,
                    url = $"/Product?brand={Uri.EscapeDataString(b.Name)}"
                })
                .ToList();

            var productResults = suggestions
                .Where(s => string.Equals(s.Type, "product", StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    price = p.Price.HasValue ? p.Price.Value.ToString("C2") : string.Empty,
                    imageUrl = string.IsNullOrWhiteSpace(p.ImageUrl) ? ProductViewModel.DefaultImageUrl : p.ImageUrl,
                    url = $"/product/p/{p.Slug}"
                })
                .ToList();

            return Json(new { categories = matchingCategories, brands = matchingBrands, products = productResults });
        }
    }
}
