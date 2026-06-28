using GameGaraj.WebUI.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace GameGaraj.WebUI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class ObservabilityAdminController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ObservabilitySettings _settings;
        private readonly ILogger<ObservabilityAdminController> _logger;

        public ObservabilityAdminController(
            IHttpClientFactory httpClientFactory,
            IOptions<ObservabilitySettings> settings,
            ILogger<ObservabilityAdminController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var serviceStatuses = new List<ServiceObservabilityStatus>();
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3); // Fast timeout for unhealthy services

            var tasks = _settings.Services.Select(async service =>
            {
                var status = new ServiceObservabilityStatus
                {
                    Key = service.Key,
                    BaseUrl = service.Value,
                    IsHealthy = false,
                    CurrentLogLevel = "Unknown",
                    TraceSamplingRatio = 0
                };

                try
                {
                    var response = await client.GetAsync($"{service.Value}/api/observability/status");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var data = JsonSerializer.Deserialize<StatusResponseDto>(content, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (data != null)
                        {
                            status.IsHealthy = true;
                            status.CurrentLogLevel = data.LogLevel;
                            status.TraceSamplingRatio = data.TraceSampling.CurrentRatio;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to fetch observability status for {Service}: {Message}", service.Key, ex.Message);
                }

                return status;
            });

            var results = await Task.WhenAll(tasks);
            serviceStatuses.AddRange(results.OrderBy(x => x.Key));

            return View(serviceStatuses);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateLogLevel(string serviceKey, string level, int durationMinutes, string reason)
        {
            if (!_settings.Services.TryGetValue(serviceKey, out var baseUrl))
            {
                TempData["Error"] = "Servis bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            using var client = _httpClientFactory.CreateClient();
            var payload = new
            {
                level = level,
                durationMinutes = durationMinutes,
                reason = reason,
                changedBy = User.Identity?.Name ?? "AdminUI"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PutAsync($"{baseUrl}/api/observability/log-level", content);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = $"{serviceKey} log seviyesi başarıyla '{level}' olarak güncellendi.";
                }
                else
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"{serviceKey} güncellenemedi: {response.StatusCode}. Detay: {errorMsg}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"{serviceKey} bağlantı hatası: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTraceSampling(string serviceKey, double ratio, int durationMinutes, string reason)
        {
            if (!_settings.Services.TryGetValue(serviceKey, out var baseUrl))
            {
                TempData["Error"] = "Servis bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            using var client = _httpClientFactory.CreateClient();
            var payload = new
            {
                ratio = ratio,
                durationMinutes = durationMinutes,
                reason = reason,
                changedBy = User.Identity?.Name ?? "AdminUI"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PutAsync($"{baseUrl}/api/observability/trace-sampling", content);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = $"{serviceKey} trace örnekleme oranı %{ratio * 100} olarak güncellendi.";
                }
                else
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"{serviceKey} güncellenemedi: {response.StatusCode}. Detay: {errorMsg}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"{serviceKey} bağlantı hatası: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> AuditLogs(string serviceKey)
        {
            if (!_settings.Services.TryGetValue(serviceKey, out var baseUrl))
            {
                return BadRequest("Servis bulunamadı.");
            }

            using var client = _httpClientFactory.CreateClient();
            try
            {
                var response = await client.GetAsync($"{baseUrl}/api/observability/audit");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var logs = JsonSerializer.Deserialize<List<AuditLogDto>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return Json(logs);
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Bağlantı hatası: {ex.Message}");
            }

            return BadRequest("Audit logları alınamadı.");
        }
    }

    // ── Helper Models & DTOs ──

    public class ServiceObservabilityStatus
    {
        public string Key { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string CurrentLogLevel { get; set; } = string.Empty;
        public double TraceSamplingRatio { get; set; }
    }

    public class StatusResponseDto
    {
        public string ServiceName { get; set; } = string.Empty;
        public string LogLevel { get; set; } = string.Empty;
        public TraceSamplingDto TraceSampling { get; set; } = new();
    }

    public class TraceSamplingDto
    {
        public double CurrentRatio { get; set; }
        public double BaselineRatio { get; set; }
    }

    public class AuditLogDto
    {
        public DateTime Timestamp { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
