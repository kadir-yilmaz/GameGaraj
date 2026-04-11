using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.ViewComponents
{
    public class CategoryMenuViewComponent : ViewComponent
    {
        private readonly ICatalogService _catalogService;

        public CategoryMenuViewComponent(ICatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var categories = await _catalogService.GetAllCategoriesAsync();
            return View(categories);
        }
    }
}
