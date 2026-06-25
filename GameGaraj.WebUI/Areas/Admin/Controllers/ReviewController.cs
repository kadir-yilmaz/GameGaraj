using GameGaraj.WebUI.Models.Reviews;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameGaraj.WebUI.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "admin, editor")]
public class ReviewController : Controller
{
    private readonly IReviewService _reviewService;

    public ReviewController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    public async Task<IActionResult> Index(int? status = null, string? q = null, int page = 1, int pageSize = 20)
    {
        ViewBag.Status = status;
        ViewBag.Query = q;
        ViewBag.StatusOptions = new List<SelectListItem>
        {
            new("Tum yorumlar", string.Empty),
            new("Onay bekleyen", "0"),
            new("Onaylanan", "1"),
            new("Reddedilen", "2")
        };

        var result = await _reviewService.GetAdminReviewsAsync(status, q, page, pageSize);
        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Moderate(ModerateReviewInput input, string? returnUrl = null)
    {
        var result = await _reviewService.ModerateAsync(input);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string reviewId, string? returnUrl = null)
    {
        var result = await _reviewService.DeleteAsAdminAsync(reviewId);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
