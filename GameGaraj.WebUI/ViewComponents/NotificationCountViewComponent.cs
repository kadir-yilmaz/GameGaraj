using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameGaraj.WebUI.ViewComponents
{
    public class NotificationCountViewComponent : ViewComponent
    {
        private readonly ICampaignService _campaignService;

        public NotificationCountViewComponent(ICampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return View(0);
            }
            var userId = UserClaimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return View(0);
            }

            var count = await _campaignService.GetUnreadNotificationCountAsync(userId);
            return View(count);
        }
    }
}
