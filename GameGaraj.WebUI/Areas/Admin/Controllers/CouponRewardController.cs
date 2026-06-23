using GameGaraj.WebUI.Models.Campaigns;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class CouponRewardController : Controller
    {
        private readonly ICampaignService _campaignService;

        public CouponRewardController(ICampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        public async Task<IActionResult> Index()
        {
            var rules = await _campaignService.GetAllRewardRulesAsync();
            return View(rules);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new CouponRewardRuleCreateInput
            {
                PeriodInDays = 30,
                IsActive = true
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CouponRewardRuleCreateInput input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var success = await _campaignService.CreateRewardRuleAsync(input);
            if (success)
            {
                TempData["SuccessMessage"] = "Kupon kazanma kuralı başarıyla oluşturuldu.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Kural oluşturulurken bir hata oluştu.");
            return View(input);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var rule = await _campaignService.GetRewardRuleByIdAsync(id);
            if (rule == null)
            {
                return NotFound();
            }
            return View(rule);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CouponRewardRuleViewModel input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var success = await _campaignService.UpdateRewardRuleAsync(input);
            if (success)
            {
                TempData["SuccessMessage"] = "Kupon kazanma kuralı başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Kural güncellenirken bir hata oluştu.");
            return View(input);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _campaignService.DeleteRewardRuleAsync(id);
            if (success)
            {
                TempData["SuccessMessage"] = "Kupon kazanma kuralı başarıyla silindi.";
            }
            else
            {
                TempData["ErrorMessage"] = "Kural silinirken bir hata oluştu.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
