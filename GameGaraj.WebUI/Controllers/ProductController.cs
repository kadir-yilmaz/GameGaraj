using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Models.Products;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers
{
    public class ProductController : Controller
    {
        private readonly ICatalogService _catalogService;
        private readonly IBasketService _basketService;
        private readonly IFavoritesService _favoritesService;

        public ProductController(ICatalogService catalogService, IBasketService basketService, IFavoritesService favoritesService)
        {
            _catalogService = catalogService;
            _basketService = basketService;
            _favoritesService = favoritesService;
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
            // Fix: Check and remove categoryId from specs
            if (specs != null)
            {
                if (specs.ContainsKey("categoryId")) specs.Remove("categoryId");
                if (specs.ContainsKey("CategoryId")) specs.Remove("CategoryId");
            }

            List<ProductViewModel> products;

            // Brand search
            if (!string.IsNullOrWhiteSpace(brand))
            {
                products = await _catalogService.SearchProductsAsync(brand);
            }
            // Keyword search
            else if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                products = await _catalogService.SearchProductsAsync(search);
                
                if (!string.IsNullOrEmpty(categoryId)) products = products.Where(p => p.CategoryId == categoryId).ToList();
                if (minPrice.HasValue) products = products.Where(p => p.Price >= minPrice.Value).ToList();
                if (maxPrice.HasValue) products = products.Where(p => p.Price <= maxPrice.Value).ToList();
            }
            else
            {
                products = await _catalogService.GetAllProductsAsync(categoryId, sortBy, minPrice, maxPrice, specs);
            }

            var categories = await _catalogService.GetAllCategoriesAsync();

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

            var basket = await _basketService.GetBasketAsync();
            var favoriteIds = await _favoritesService.GetFavoriteProductIdsAsync();
            var basketProductIds = basket?.Items?
                .Select(x => x.ProductId?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var product in products)
            {
                product.IsInBasket = basketProductIds.Contains(product.Id?.Trim() ?? string.Empty);
                product.IsFavorite = favoriteIds.Contains(product.Id);
            }

            return View(products);
        }

        [Route("product/p/{slug}")]
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
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            product.IsInBasket = basketProductIds.Contains(product.Id?.Trim() ?? string.Empty);

            product.IsFavorite = await _favoritesService.IsFavoriteAsync(product.Id);

            return View(product);
        }

        [HttpGet("api/products/search")]
        public async Task<IActionResult> SearchProducts(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Json(new { categories = new List<object>(), products = new List<object>() });
            }

            var categories = await _catalogService.SearchCategoriesAsync(q);
            var matchingCategories = categories
                .Take(3)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    url = $"/product/c/{c.Slug}"
                })
                .ToList();

            var brands = await _catalogService.SearchBrandsAsync(q);
            var matchingBrands = brands
                .Take(3)
                .Select(b => new
                {
                    id = b,
                    name = b,
                    url = $"/Product?brand={Uri.EscapeDataString(b)}"
                })
                .ToList();

            var products = await _catalogService.SearchProductsAsync(q);
            var productResults = products
                .Take(5)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    price = p.Price.ToString("C2"),
                    imageUrl = p.FirstImageUrl,
                    url = $"/product/p/{p.Slug}"
                })
                .ToList();

            return Json(new { categories = matchingCategories, brands = matchingBrands, products = productResults });
        }
    }
}
