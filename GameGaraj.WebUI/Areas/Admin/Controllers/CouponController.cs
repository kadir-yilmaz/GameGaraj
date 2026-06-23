using GameGaraj.WebUI.Models.Campaigns;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class CouponController : Controller
    {
        private readonly ICampaignService _campaignService;
        private readonly IIdentityService _identityService;

        public CouponController(ICampaignService campaignService, IIdentityService identityService)
        {
            _campaignService = campaignService;
            _identityService = identityService;
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Json(new List<object>());
            }

            var users = await _identityService.SearchUsersAsync(q);
            return Json(users.Select(u => new { id = u.Id, email = u.Email, displayName = u.DisplayName }));
        }

        public async Task<IActionResult> Index()
        {
            var coupons = await _campaignService.GetAllCouponsAsync();
            return View(coupons);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new CouponCreateInput
            {
                ExpiryDate = DateTime.Now.AddDays(30)
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CouponCreateInput input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            if (!string.IsNullOrEmpty(input.Code))
            {
                input.Code = input.Code.ToUpperInvariant();
            }

            var success = await _campaignService.CreateCouponAsync(input);
            if (success)
            {
                TempData["SuccessMessage"] = "Kupon başarıyla oluşturuldu.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Kupon oluşturulurken bir hata oluştu. Kod benzersiz olmalıdır.");
            return View(input);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _campaignService.DeleteCouponAsync(id);
            if (success)
            {
                TempData["SuccessMessage"] = "Kupon başarıyla silindi.";
            }
            else
            {
                TempData["ErrorMessage"] = "Kupon silinirken bir hata oluştu.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
