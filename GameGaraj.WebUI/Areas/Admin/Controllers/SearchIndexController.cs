using GameGaraj.WebUI.Areas.Admin.Models;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin, editor")]
    public class SearchIndexController : Controller
    {
        private readonly ICatalogService _catalogService;

        public SearchIndexController(ICatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        public async Task<IActionResult> Index([FromQuery] int page = 1)
        {
            var model = new SearchIndexDashboardViewModel
            {
                Status = await _catalogService.GetSearchIndexStatusAsync(),
                Documents = await _catalogService.GetSearchIndexDocumentPreviewsAsync(page, 100)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rebuild()
        {
            var result = await _catalogService.ReindexSearchIndexAsync();

            if (result == null)
            {
                TempData["Error"] = "Arama indeksi yeniden oluşturulamadı.";
                return RedirectToAction(nameof(Index));
            }

            TempData[result.Failed == 0 ? "Success" : "Error"] =
                $"İndeksleme tamamlandı. Başarılı: {result.Succeeded}, Başarısız: {result.Failed}, Toplam: {result.Total}.";

            return RedirectToAction(nameof(Index));
        }
    }
}
