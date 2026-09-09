using AspNetCoreHero.ToastNotification.Abstractions;
using GameGaraj.WebUI.Extensions;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Models.Reviews;
using Microsoft.AspNetCore.Mvc;

using GameGaraj.WebUI.Models.Discussion;

namespace GameGaraj.WebUI.Controllers
{
    public class ProductController : Controller
    {
        private readonly ICatalogService _catalogService;
        private readonly ISearchService _searchService;
        private readonly IBasketService _basketService;
        private readonly IFavoritesService _favoritesService;
        private readonly IReviewService _reviewService;
        private readonly IDiscussionService _discussionService;
        private readonly INotyfService _notyf;
        private readonly ILogger<ProductController> _logger;

        public ProductController(
            ICatalogService catalogService,
            ISearchService searchService,
            IBasketService basketService,
            IFavoritesService favoritesService,
            IReviewService reviewService,
            IDiscussionService discussionService,
            INotyfService notyf,
            ILogger<ProductController> logger)
        {
            _catalogService = catalogService;
            _searchService = searchService;
            _basketService = basketService;
            _favoritesService = favoritesService;
            _reviewService = reviewService;
            _discussionService = discussionService;
            _notyf = notyf;
            _logger = logger;
        }

        /// <summary>
        /// Category page: /{slug}-c-{categoryId}
        /// e.g. /ram-c-6832abc, /ram-c-6832abc?marka=Samsung&siralama=fiyat-artan
        /// </summary>
        public async Task<IActionResult> Category(string slug, string? marka, string? siralama,
            decimal? minFiyat, decimal? maxFiyat, Dictionary<string, string[]>? specs)
        {
            var (categorySlug, categoryId) = SlugHelper.ParseCategorySlug(slug);

            if (string.IsNullOrEmpty(categoryId))
                return NotFound();

            // Parallel fetch category, categories, and products
            CleanSpecs(specs);
            var sortBy = MapSortBy(siralama);

            var categoryTask = _catalogService.GetCategoryByIdAsync(categoryId);
            var categoriesTask = _catalogService.GetAllCategoriesAsync();
            var productsTask = _catalogService.GetAllProductsAsync(categoryId, sortBy, minFiyat, maxFiyat, specs, marka);

            await Task.WhenAll(categoryTask, categoriesTask, productsTask);

            var categoryModel = categoryTask.Result;
            if (categoryModel == null)
                return NotFound();

            // Verify slug matches, canonical 301 redirect to correct URL if needed (Hepsiburada style)
            if (!string.Equals(categorySlug, categoryModel.Slug, StringComparison.OrdinalIgnoreCase))
            {
                var targetUrl = SlugHelper.BuildCategoryUrl(categoryModel.Slug, categoryId) + Request.QueryString.Value;
                return RedirectPermanent(targetUrl);
            }

            var categories = categoriesTask.Result ?? new List<CategoryViewModel>();
            var products = productsTask.Result ?? new List<ProductViewModel>();

            SetupViewBags(categoryModel, categoryId, categories, products,
                sortBy, minFiyat, maxFiyat, specs, null, marka, siralama);

            ViewBag.CurrentBaseUrl = SlugHelper.BuildCategoryUrl(categoryModel.Slug, categoryId);

            await Task.WhenAll(ApplyProductState(products), ApplyReviewSummariesAsync(products));

            return View("Index", products);
        }

        /// <summary>
        /// Search & All Products page: /ara, /ara?q=iphone+17, /ara?marka=Samsung
        /// </summary>
        [Route("ara")]
        public async Task<IActionResult> Search(string? q, string? marka, string? siralama,
            decimal? minFiyat, decimal? maxFiyat, Dictionary<string, string[]>? specs, string? categoryId)
        {
            CleanSpecs(specs);
            var sortBy = MapSortBy(siralama);

            Task<CategoryViewModel?> categoryTask = !string.IsNullOrEmpty(categoryId) 
                ? _catalogService.GetCategoryByIdAsync(categoryId) 
                : Task.FromResult<CategoryViewModel?>(null);
            Task<List<CategoryViewModel>> categoriesTask = _catalogService.GetAllCategoriesAsync();

            List<ProductViewModel> products;

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                var searchTask = _searchService.SearchProductsAsync(q);
                await Task.WhenAll(categoryTask, categoriesTask, searchTask);

                products = searchTask.Result ?? new List<ProductViewModel>();
                if (!products.Any())
                {
                    products = await _catalogService.SearchProductsAsync(q) ?? new List<ProductViewModel>();
                }

                if (!string.IsNullOrEmpty(categoryId)) products = products.Where(p => p.CategoryId == categoryId).ToList();
                if (minFiyat.HasValue) products = products.Where(p => p.Price >= minFiyat.Value).ToList();
                if (maxFiyat.HasValue) products = products.Where(p => p.Price <= maxFiyat.Value).ToList();
                if (!string.IsNullOrWhiteSpace(marka))
                {
                    var normalizedBrand = marka.Trim();
                    products = products.Where(p =>
                        string.Equals(p.Brand?.Trim(), normalizedBrand, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(p.Name) &&
                            p.Name.Trim().StartsWith($"{normalizedBrand} ", StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                }
            }
            else
            {
                var productsTask = _catalogService.GetAllProductsAsync(categoryId, sortBy, minFiyat, maxFiyat, specs, marka);
                await Task.WhenAll(categoryTask, categoriesTask, productsTask);
                products = productsTask.Result ?? new List<ProductViewModel>();
            }

            var categoryModel = categoryTask.Result;
            var categories = categoriesTask.Result ?? new List<CategoryViewModel>();

            SetupViewBags(categoryModel, categoryId, categories, products,
                sortBy, minFiyat, maxFiyat, specs, q, marka, siralama);

            ViewBag.CurrentBaseUrl = "/ara";

            await Task.WhenAll(ApplyProductState(products), ApplyReviewSummariesAsync(products));

            return View("Index", products);
        }

        /// <summary>
        /// Product detail page: /{slug}-p-{productId}
        /// e.g. /samsung-galaxy-s24-ultra-256gb-p-abc123
        /// </summary>
        public async Task<IActionResult> Detail(string slug)
        {
            var (productSlug, productId) = SlugHelper.ParseProductSlug(slug);

            if (string.IsNullOrEmpty(productId))
                return NotFound();

            var product = await _catalogService.GetProductByIdAsync(productId);
            if (product == null)
                return NotFound();

            // Verify slug matches, canonical 301 redirect to correct URL if needed (Hepsiburada style)
            if (!string.Equals(productSlug, product.Slug, StringComparison.OrdinalIgnoreCase))
            {
                var targetUrl = SlugHelper.BuildProductUrl(product.Slug, productId) + Request.QueryString.Value;
                return RedirectPermanent(targetUrl);
            }

            var pid = product.Id?.Trim() ?? string.Empty;

            // Parallelize all supplementary lookups (basket, favorites, reviews, questions, permissions, category)
            var basketTask = _basketService.GetBasketAsync();
            var favTask = _favoritesService.IsFavoriteAsync(pid);
            var reviewsTask = _reviewService.GetProductReviewsAsync(pid, 0, 10);
            var questionsTask = _discussionService.GetProductQuestionsAsync(pid, 0, 20);
            var canReviewTask = User.Identity?.IsAuthenticated == true ? _reviewService.CanReviewAsync(pid) : Task.FromResult(new CanReviewViewModel { CanReview = false });
            var userReviewTask = User.Identity?.IsAuthenticated == true ? _reviewService.GetUserReviewAsync(pid) : Task.FromResult(new UserReviewResponseViewModel());
            var categoryTask = !string.IsNullOrEmpty(product.CategoryId) ? _catalogService.GetCategoryByIdAsync(product.CategoryId) : Task.FromResult<CategoryViewModel?>(null);

            await Task.WhenAll(basketTask, favTask, reviewsTask, questionsTask, canReviewTask, userReviewTask, categoryTask);

            var basket = basketTask.Result;
            var basketProductIds = basket?.Items?
                .Select(x => x.ProductId?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            product.IsInBasket = basketProductIds.Contains(pid);
            product.IsFavorite = favTask.Result;

            ViewBag.Reviews = reviewsTask.Result;
            ViewBag.Questions = questionsTask.Result;

            if (User.Identity?.IsAuthenticated == true)
            {
                ViewBag.CanReview = canReviewTask.Result;
                ViewBag.UserReview = userReviewTask.Result;
            }

            ViewBag.CategorySlug = categoryTask.Result?.Slug;

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
        public async Task<IActionResult> CreateQuestion(CreateQuestionInput input, string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                _notyf.Warning("Soru sormak için lütfen giriş yapın.");
                return RedirectToAction("SignIn", "Auth", new { returnUrl });
            }

            var result = await _discussionService.CreateQuestionAsync(input);
            if (result.Succeeded)
            {
                _notyf.Success(result.Message);
            }
            else
            {
                _notyf.Error(string.IsNullOrWhiteSpace(result.Message) ? "Soru gönderilemedi." : result.Message);
            }

            return RedirectToLocalOrProduct(input.ProductId, returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestionAjax(CreateQuestionInput input)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Json(new { succeeded = false, message = "Soru sormak için lütfen giriş yapın.", requireAuth = true });
            }

            var result = await _discussionService.CreateQuestionAsync(input);
            return Json(new
            {
                succeeded = result.Succeeded,
                message = result.Message,
                id = result.Id,
                productId = input.ProductId,
                questionText = input.QuestionText,
                userName = User.Identity?.Name ?? "Kullanıcı"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoteAnswer(string answerId, bool isHelpful)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Json(new { succeeded = false, message = "Oy kullanmak için lütfen giriş yapın.", requireAuth = true });
            }

            var result = await _discussionService.VoteAnswerAsync(answerId, isHelpful);
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetMoreQuestions(string productId, int page = 1, int pageSize = 10)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                return BadRequest();
            }

            var questions = await _discussionService.GetProductQuestionsAsync(productId, page, pageSize);
            return Json(questions);
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

        private async Task<IActionResult> RedirectToProductDetail(string productId)
        {
            var product = await _catalogService.GetProductByIdAsync(productId);
            if (product != null && !string.IsNullOrEmpty(product.Slug))
            {
                return Redirect(SlugHelper.BuildProductUrl(product.Slug, product.Id));
            }
            return Redirect($"/ara");
        }

        private IActionResult RedirectToLocalOrProduct(string productId, string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            // We don't have slug here, redirect to the product page via an intermediate lookup
            return RedirectToProductDetail(productId).GetAwaiter().GetResult();
        }

        [HttpGet("api/products/search")]
        public async Task<IActionResult> SearchProducts(string q)
        {
            q = q?.Trim() ?? string.Empty;

            if (q.Length < 2)
            {
                return Json(new { categories = new List<object>(), brands = new List<object>(), products = new List<object>() });
            }

            var suggestions = await _searchService.SearchSuggestionsAsync(q);
            if (suggestions == null || !suggestions.Any())
            {
                suggestions = await _catalogService.SearchSuggestionsAsync(q);
            }

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
                            url = SlugHelper.BuildCategoryUrl(c.Slug, c.Id)
                        }),
                    brands = fallbackBrands
                        .Take(3)
                        .Select(b => new
                        {
                            id = b,
                            name = b,
                            url = $"/ara?marka={Uri.EscapeDataString(b)}"
                        }),
                    products = fallbackProducts
                        .Take(10)
                        .Select(p => new
                        {
                            id = p.Id,
                            name = p.Name,
                            price = p.Price.ToString("C2"),
                            imageUrl = p.FirstImageUrl,
                            url = SlugHelper.BuildSearchUrl(p.Name)
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
                        ? SlugHelper.BuildCategoryUrl(c.Slug, c.Id)
                        : $"/ara?categoryId={Uri.EscapeDataString(c.Id)}"
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
                    url = $"/ara?marka={Uri.EscapeDataString(b.Name)}"
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
                    url = SlugHelper.BuildSearchUrl(p.Name)
                })
                .ToList();

            return Json(new { categories = matchingCategories, brands = matchingBrands, products = productResults });
        }

        #region Private Helpers

        private static void CleanSpecs(Dictionary<string, string[]>? specs)
        {
            if (specs == null) return;

            var reservedKeys = new[]
            {
                "category", "categoryId", "CategoryId", "sortBy", "siralama",
                "minPrice", "maxPrice", "minFiyat", "maxFiyat",
                "search", "q", "brand", "marka", "slug"
            };

            foreach (var key in reservedKeys)
            {
                specs.Remove(key);
            }
        }

        private static string? MapSortBy(string? siralama)
        {
            return siralama switch
            {
                "fiyat-artan" => "price_asc",
                "fiyat-azalan" => "price_desc",
                "isim-a-z" => "name_asc",
                _ => null
            };
        }

        private static string? MapSortByReverse(string? sortBy)
        {
            return sortBy switch
            {
                "price_asc" => "fiyat-artan",
                "price_desc" => "fiyat-azalan",
                "name_asc" => "isim-a-z",
                _ => null
            };
        }

        private void SetupViewBags(
            CategoryViewModel? categoryModel, string? categoryId,
            List<CategoryViewModel> categories, List<ProductViewModel> brandSourceProducts,
            string? sortBy, decimal? minFiyat, decimal? maxFiyat,
            Dictionary<string, string[]>? specs, string? search, string? marka, string? siralama)
        {
            ViewBag.CurrentCategoryName = categoryModel?.Name ?? (string.IsNullOrWhiteSpace(search) ? "Tüm Ürünler" : $"\"{search}\" araması");
            ViewBag.CurrentCategoryAttributes = categoryModel?.Attributes;
            ViewBag.CategoryId = categoryId;
            ViewBag.CategorySlug = categoryModel?.Slug;
            ViewBag.Categories = categories;
            ViewBag.SortBy = sortBy;
            ViewBag.Siralama = siralama;
            ViewBag.MinPrice = minFiyat;
            ViewBag.MaxPrice = maxFiyat;
            ViewBag.SelectedSpecs = specs ?? new Dictionary<string, string[]>();
            ViewBag.Search = search;
            ViewBag.Brand = marka;
            ViewBag.Brands = brandSourceProducts
                .Select(p => p.Brand)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        private async Task ApplyProductState(List<ProductViewModel> products)
        {
            if (products == null || products.Count == 0) return;

            var basketTask = _basketService.GetBasketAsync();
            var favTask = _favoritesService.GetFavoriteProductIdsAsync();
            await Task.WhenAll(basketTask, favTask);

            var basket = basketTask.Result;
            var favoriteIds = favTask.Result != null 
                ? new HashSet<string>(favTask.Result, StringComparer.OrdinalIgnoreCase) 
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        #endregion
    }
}
