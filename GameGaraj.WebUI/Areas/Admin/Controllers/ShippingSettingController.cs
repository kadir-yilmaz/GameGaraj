using GameGaraj.WebUI.Models.Campaigns;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class ShippingSettingController : Controller
    {
        private readonly ICampaignService _campaignService;

        public ShippingSettingController(ICampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        public async Task<IActionResult> Index()
        {
            var setting = await _campaignService.GetShippingSettingAsync();
            if (setting == null)
            {
                // API cevap vermezse veya tablo boşsa default değer çıkar
                setting = new ShippingSettingViewModel
                {
                    FreeShippingThreshold = 1000,
                    DefaultShippingFee = 50,
                    IsActive = true
                };
            }
            return View(setting);
        }

        [HttpPost]
        public async Task<IActionResult> Index(ShippingSettingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var success = await _campaignService.UpdateShippingSettingAsync(model);
            
            if (success)
            {
                TempData["SuccessMessage"] = "Kargo ayarları başarıyla güncellendi.";
            }
            else
            {
                TempData["ErrorMessage"] = "Kargo ayarları güncellenirken bir hata oluştu!";
            }

            return View(model);
        }
    }
}
