using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.ViewComponents
{
    public class BasketCountViewComponent : ViewComponent
    {
        private readonly IBasketService _basketService;

        public BasketCountViewComponent(IBasketService basketService)
        {
            _basketService = basketService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var basket = await _basketService.GetBasketAsync();
            var count = basket?.Items?.Sum(x => x.Quantity) ?? 0;
            return View(count);
        }
    }
}
