using GameGaraj.WebUI.Models.Discounts;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class DiscountController : Controller
    {
        private readonly IDiscountService _discountService;

        public DiscountController(IDiscountService discountService)
        {
            _discountService = discountService;
        }

        public async Task<IActionResult> Index()
        {
            var discounts = await _discountService.GetAllAsync();
            return View(discounts);
        }

        [HttpGet]
        public IActionResult Create()
        {
            // Preset an expiration date to 30 days from now for convenience
            var model = new CreateDiscountInput
            {
                ExpirationDate = DateTime.Now.AddDays(30)
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateDiscountInput input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            // Ensure UserId is not null for API
            if (string.IsNullOrEmpty(input.UserId))
            {
                input.UserId = string.Empty;
            }

            var success = await _discountService.SaveAsync(input);
            if (success)
            {
                TempData["SuccessMessage"] = "İndirim kuponu başarıyla oluşturuldu.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Kupon oluşturulurken bir hata oluştu. Kod benzersiz olmalıdır.");
            return View(input);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var discount = await _discountService.GetByIdAsync(id);
            if (discount == null)
            {
                return NotFound();
            }

            var input = new UpdateDiscountInput
            {
                Id = discount.Id,
                UserId = discount.UserId,
                Rate = discount.Rate,
                Code = discount.Code,
                CreatedTime = discount.CreatedTime,
                ExpirationDate = discount.ExpirationDate,
                AllowedProductIds = discount.AllowedProductIds
            };
            
            return View(input);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(UpdateDiscountInput input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            if (string.IsNullOrEmpty(input.UserId))
            {
                input.UserId = string.Empty;
            }

            var success = await _discountService.UpdateAsync(input);
            if (success)
            {
                TempData["SuccessMessage"] = "İndirim kuponu başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Kupon güncellenirken bir hata oluştu.");
            return View(input);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _discountService.DeleteAsync(id);
            if (success)
            {
                TempData["SuccessMessage"] = "İndirim kuponu başarıyla silindi.";
            }
            else
            {
                TempData["ErrorMessage"] = "Kupon silinirken bir hata oluştu.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
