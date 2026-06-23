using GameGaraj.WebUI.Models.Campaigns;
using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin, editor")]
    public class CarouselController : Controller
    {
        private readonly ICampaignService _campaignService;
        private readonly IPhotoStockService _photoStockService;
        private readonly ILogger<CarouselController> _logger;

        public CarouselController(ICampaignService campaignService, IPhotoStockService photoStockService, ILogger<CarouselController> logger)
        {
            _campaignService = campaignService;
            _photoStockService = photoStockService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var images = await _campaignService.GetCarouselImagesAsync();
            return View(images);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormFile? photo, string? imageUrl, int displayOrder)
        {
            string finalImageUrl = string.Empty;

            if (photo != null && photo.Length > 0)
            {
                try
                {
                    // Create a FormFileCollection with a single file
                    var files = new FormFileCollection { photo };
                    var uploadedUrls = await _photoStockService.UploadPhotosAsync(files, "carousel", "bg");
                    if (uploadedUrls != null && uploadedUrls.Count > 0)
                    {
                        finalImageUrl = uploadedUrls[0];
                    }
                    else
                    {
                        TempData["Error"] = "Görsel yüklenemedi. PhotoStock API hatası.";
                        return RedirectToAction(nameof(Index));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Carousel image upload failed");
                    TempData["Error"] = $"Görsel yüklenirken hata oluştu: {ex.Message}";
                    return RedirectToAction(nameof(Index));
                }
            }
            else if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                finalImageUrl = imageUrl.Trim();
            }
            else
            {
                TempData["Error"] = "Lütfen bir görsel yükleyin veya görsel URL'si girin.";
                return RedirectToAction(nameof(Index));
            }

            var model = new CarouselImageViewModel
            {
                ImageUrl = finalImageUrl,
                DisplayOrder = displayOrder,
                CreatedTime = DateTime.UtcNow
            };

            var success = await _campaignService.CreateCarouselImageAsync(model);
            if (success)
            {
                TempData["Success"] = "Carousel görseli başarıyla eklendi.";
            }
            else
            {
                TempData["Error"] = "Carousel görseli veritabanına kaydedilemedi.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string imageUrl)
        {
            if (id <= 0)
            {
                TempData["Error"] = "Geçersiz görsel ID'si.";
                return RedirectToAction(nameof(Index));
            }

            // Önce Campaign API'den silelim
            var success = await _campaignService.DeleteCarouselImageAsync(id);
            if (success)
            {
                TempData["Success"] = "Carousel görseli başarıyla silindi.";

                // Eğer resim bizim PhotoStock API üzerinden yüklenmişse (sunucu üzerinde barınıyorsa), onu da temizleyelim
                if (!string.IsNullOrWhiteSpace(imageUrl) && imageUrl.Contains("/photos/", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await _photoStockService.DeletePhotoAsync(imageUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PhotoStock delete failed for: {Url}", imageUrl);
                    }
                }
            }
            else
            {
                TempData["Error"] = "Carousel görseli silinirken bir hata oluştu.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
