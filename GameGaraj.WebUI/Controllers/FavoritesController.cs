using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers
{
    public class FavoritesController : Controller
    {
        private readonly IFavoritesService _favoritesService;
        private readonly ICatalogService _catalogService;
        private readonly IReviewService _reviewService;
        private readonly ILogger<FavoritesController> _logger;

        public FavoritesController(IFavoritesService favoritesService, ICatalogService catalogService, IReviewService reviewService, ILogger<FavoritesController> logger)
        {
            _favoritesService = favoritesService;
            _catalogService = catalogService;
            _reviewService = reviewService;
            _logger = logger;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var favoriteIds = await _favoritesService.GetFavoriteProductIdsAsync();

            if (!favoriteIds.Any())
            {
                return View(new List<Models.Products.ProductViewModel>());
            }

            // Get all products and filter by favorite IDs
            var allProducts = await _catalogService.GetAllProductsAsync(null, null, null, null, null);
            var favoriteProducts = allProducts
                .Where(p => favoriteIds.Contains(p.Id))
                .ToList();

            // Mark all as favorites
            foreach (var product in favoriteProducts)
            {
                product.IsFavorite = true;
            }
            await ApplyReviewSummariesAsync(favoriteProducts);

            return View(favoriteProducts);
        }

        private async Task ApplyReviewSummariesAsync(List<Models.Products.ProductViewModel> products)
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

        [HttpPost]
        public async Task<IActionResult> Toggle(string id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Lütfen önce giriş yapın." });
                }
                return RedirectToAction("SignIn", "Auth");
            }

            _logger.LogInformation("[FavoritesController] Toggle favorite for product: {ProductId}", id);

            var isFavorite = await _favoritesService.IsFavoriteAsync(id);

            bool success;
            if (isFavorite)
            {
                success = await _favoritesService.RemoveFavoriteAsync(id);
                isFavorite = false;
            }
            else
            {
                success = await _favoritesService.AddFavoriteAsync(id);
                isFavorite = true;
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success, isFavorite });
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Add(string id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Lütfen önce giriş yapın." });
                }
                return RedirectToAction("SignIn", "Auth");
            }

            var success = await _favoritesService.AddFavoriteAsync(id);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success, isFavorite = true });
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Remove(string id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Lütfen önce giriş yapın." });
                }
                return RedirectToAction("SignIn", "Auth");
            }

            var success = await _favoritesService.RemoveFavoriteAsync(id);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success, isFavorite = false });
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> GetFavorites()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Json(new { favorites = new List<string>() });
            }

            var favorites = await _favoritesService.GetFavoriteProductIdsAsync();
            return Json(new { favorites });
        }
    }
}
