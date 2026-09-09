using GameGaraj.WebUI.Models.Discussion;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameGaraj.WebUI.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "admin")]
public class DiscussionController : Controller
{
    private readonly IDiscussionService _discussionService;

    public DiscussionController(IDiscussionService discussionService)
    {
        _discussionService = discussionService;
    }

    public async Task<IActionResult> Index(int? status = null, bool? archived = null, string? q = null, int page = 1, int pageSize = 20)
    {
        ViewBag.Status = status;
        ViewBag.Archived = archived;
        ViewBag.Query = q;
        ViewBag.StatusOptions = new List<SelectListItem>
        {
            new("Tüm sorular", string.Empty),
            new("Cevap bekleyen", "0"),
            new("Cevaplanmış", "1")
        };

        var result = await _discussionService.GetAdminQuestionsAsync(status, archived, q, page, pageSize);
        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Answer(AnswerQuestionInput input, string? returnUrl = null)
    {
        var result = await _discussionService.AnswerQuestionAsync(input.QuestionId, input.AnswerText);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string questionId, string? returnUrl = null)
    {
        var result = await _discussionService.DeleteQuestionAsAdminAsync(questionId);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
