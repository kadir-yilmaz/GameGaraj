using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.ViewComponents
{
    public class FavoritesCountViewComponent : ViewComponent
    {
        private readonly IFavoritesService _favoritesService;

        public FavoritesCountViewComponent(IFavoritesService favoritesService)
        {
            _favoritesService = favoritesService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var favorites = await _favoritesService.GetFavoriteProductIdsAsync();
            var count = favorites?.Count ?? 0;
            return View(count);
        }
    }
}
