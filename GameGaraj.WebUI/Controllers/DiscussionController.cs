using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Controllers
{
    [Authorize]
    public class DiscussionController : Controller
    {
        private readonly IDiscussionService _discussionService;

        public DiscussionController(IDiscussionService discussionService)
        {
            _discussionService = discussionService;
        }

        [HttpGet]
        public async Task<IActionResult> MyQuestions()
        {
            var questions = await _discussionService.GetMyQuestionsAsync();
            return View(questions);
        }
    }
}
