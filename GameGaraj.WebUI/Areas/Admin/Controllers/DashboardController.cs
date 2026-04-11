using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin, editor")]
    public class DashboardController : Controller
    {
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ILogger<DashboardController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            _logger.LogInformation("[Admin Dashboard] Dashboard accessed");
            return View();
        }
    }
}
