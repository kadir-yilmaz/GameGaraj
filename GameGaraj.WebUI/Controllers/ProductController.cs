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

        public async Task<IActionResult> Index(string? categoryId, string? sortBy, decimal? minPrice, decimal? maxPrice, Dictionary<string, string[]>? specs, string? search, string? brand)
        {
            // Sanitize CategoryId (Remove accidental leading dashes or non-alphanumeric prefixes)
            if (!string.IsNullOrEmpty(categoryId))
            {
                // Regex to keep only valid hex chars if it's a 24-char ObjectId
                categoryId = System.Text.RegularExpressions.Regex.Replace(categoryId, @"^[^0-9a-fA-F]+", "");
            }

            // Fix: Check and remove categoryId from specs if it accidentally got in there due to model binding or query string issues
            if (specs != null)
            {
                if (specs.ContainsKey("categoryId")) specs.Remove("categoryId");
                if (specs.ContainsKey("CategoryId")) specs.Remove("CategoryId");
            }

            List<ProductViewModel> products;

            // Brand search: use brand as keyword in Elasticsearch
            if (!string.IsNullOrWhiteSpace(brand))
            {
                products = await _catalogService.SearchProductsAsync(brand);
                // Elasticsearch already handles fuzzy matching on brand field
            }
            // Keyword search
            else if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                products = await _catalogService.SearchProductsAsync(search);
                
                // Apply remaining filters in-memory for search results
                if (!string.IsNullOrEmpty(categoryId)) products = products.Where(p => p.CategoryId == categoryId).ToList();
                if (minPrice.HasValue) products = products.Where(p => p.Price >= minPrice.Value).ToList();
                if (maxPrice.HasValue) products = products.Where(p => p.Price <= maxPrice.Value).ToList();
            }
            else
            {
                products = await _catalogService.GetAllProductsAsync(categoryId, sortBy, minPrice, maxPrice, specs);
            }
            var categories = await _catalogService.GetAllCategoriesAsync();

            // Explicitly set CurrentCategoryName for the view header
            ViewBag.CurrentCategoryName = "Tüm Ürünler";

            if (!string.IsNullOrEmpty(categoryId))
            {
                var category = await _catalogService.GetCategoryByIdAsync(categoryId);
                if (category != null)
                {
                    ViewBag.CurrentCategoryAttributes = category.Attributes;
                    ViewBag.CurrentCategoryName = category.Name;
                    ViewBag.CategoryId = categoryId; // Use the sanitized one
                }
                else
                {
                    ViewBag.CurrentCategoryName = "Kategori Bulunamadı";
                }
            }
            else
            {
                ViewBag.CategoryId = null;
            }
            ViewBag.Categories = categories;
            ViewBag.SortBy = sortBy;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.SelectedSpecs = specs ?? new Dictionary<string, string[]>();
            ViewBag.Search = search;

            var basket = await _basketService.GetBasketAsync();
            var favoriteIds = await _favoritesService.GetFavoriteProductIdsAsync();
            
            foreach (var product in products)
            {
                if (basket != null && basket.Items.Any())
                {
                    product.IsInBasket = basket.Items.Any(x => x.ProductId == product.Id);
                }
                product.IsFavorite = favoriteIds.Contains(product.Id);
            }

            return View(products);
        }

        public async Task<IActionResult> Detail(string id)
        {
            var product = await _catalogService.GetProductByIdAsync(id);

            if (product == null)
                return NotFound();

            var basket = await _basketService.GetBasketAsync();
            if (basket != null && basket.Items.Any(x => x.ProductId == product.Id))
            {
                product.IsInBasket = true;
            }

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
                    url = $"/Product?categoryId={c.Id}"
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
                    url = $"/Product/Detail/{p.Id}"
                })
                .ToList();

            return Json(new { categories = matchingCategories, brands = matchingBrands, products = productResults });
        }
    }
}
