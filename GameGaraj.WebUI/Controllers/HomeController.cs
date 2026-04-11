using GameGaraj.WebUI.Models;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GameGaraj.WebUI.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ICatalogService _catalogService;
        private readonly IBasketService _basketService;
        private readonly IFavoritesService _favoritesService;

        public HomeController(ILogger<HomeController> logger, ICatalogService catalogService, IBasketService basketService, IFavoritesService favoritesService)
        {
            _logger = logger;
            _catalogService = catalogService;
            _basketService = basketService;
            _favoritesService = favoritesService;
        }

        public async Task<IActionResult> Index()
        {
            var featuredProducts = await _catalogService.GetFeaturedProductsAsync();
            var basket = await _basketService.GetBasketAsync();
            var favoriteIds = await _favoritesService.GetFavoriteProductIdsAsync();

            foreach (var product in featuredProducts)
            {
                if (basket != null && basket.Items.Any())
                {
                    product.IsInBasket = basket.Items.Any(x => x.ProductId == product.Id);
                }
                product.IsFavorite = favoriteIds.Contains(product.Id);
            }

            return View(featuredProducts);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
