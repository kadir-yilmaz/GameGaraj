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
            var viewModel = new ObservabilityDashboardViewModel();
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3); // Fast timeout for unhealthy services

            // 1. Fetch Microservice Statuses
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
            viewModel.Services = results.OrderBy(x => x.Key).ToList();

            // 2. Fetch Elasticsearch Indices matching gamegaraj-logs-*
            if (!string.IsNullOrEmpty(_settings.ElasticSearchUri))
            {
                try
                {
                    var esResponse = await client.GetAsync($"{_settings.ElasticSearchUri}/_cat/indices/gamegaraj-logs-*?format=json&s=index");
                    if (esResponse.IsSuccessStatusCode)
                    {
                        var esContent = await esResponse.Content.ReadAsStringAsync();
                        var indices = JsonSerializer.Deserialize<List<ElasticIndexDto>>(esContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (indices != null)
                        {
                            viewModel.ElasticIndices = indices.OrderByDescending(x => x.Index).ToList();
                        }
                    }

                    // Check ILM status
                    var ilmResponse = await client.GetAsync($"{_settings.ElasticSearchUri}/_ilm/policy/gamegaraj-logs-policy");
                    viewModel.IsIlmConfigured = ilmResponse.IsSuccessStatusCode;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to fetch Elasticsearch data from {Uri}: {Message}", _settings.ElasticSearchUri, ex.Message);
                }
            }

            return View(viewModel);
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

        [HttpPost]
        public async Task<IActionResult> SyncElasticsearchIlm()
        {
            if (string.IsNullOrEmpty(_settings.ElasticSearchUri))
            {
                TempData["Error"] = "ElasticSearchUri ayarı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                // 1. Create ILM Policy
                var policyJson = @"{
                  ""policy"": {
                    ""phases"": {
                      ""hot"": {
                        ""min_age"": ""0ms"",
                        ""actions"": {
                          ""rollover"": {
                            ""max_age"": ""1d"",
                            ""max_primary_shard_size"": ""5gb""
                          },
                          ""set_priority"": {
                            ""priority"": 100
                          }
                        }
                      },
                      ""warm"": {
                        ""min_age"": ""7d"",
                        ""actions"": {
                          ""shrink"": {
                            ""number_of_shards"": 1
                          },
                          ""forcemerge"": {
                            ""max_num_segments"": 1
                          },
                          ""set_priority"": {
                            ""priority"": 50
                          }
                        }
                      },
                      ""cold"": {
                        ""min_age"": ""30d"",
                        ""actions"": {
                          ""set_priority"": {
                            ""priority"": 0
                          }
                        }
                      },
                      ""delete"": {
                        ""min_age"": ""180d"",
                        ""actions"": {
                          ""delete"": {}
                        }
                      }
                    }
                  }
                }";

                var policyContent = new StringContent(policyJson, Encoding.UTF8, "application/json");
                var policyResponse = await client.PutAsync($"{_settings.ElasticSearchUri}/_ilm/policy/gamegaraj-logs-policy", policyContent);

                if (!policyResponse.IsSuccessStatusCode)
                {
                    var error = await policyResponse.Content.ReadAsStringAsync();
                    TempData["Error"] = $"ILM Politikasını oluştururken hata alındı: {error}";
                    return RedirectToAction(nameof(Index));
                }

                // 2. Create Index Template
                var templateJson = @"{
                  ""index_patterns"": [""gamegaraj-logs-*""],
                  ""template"": {
                    ""settings"": {
                      ""index.lifecycle.name"": ""gamegaraj-logs-policy"",
                      ""index.lifecycle.rollover_alias"": ""gamegaraj-logs"",
                      ""number_of_shards"": 1,
                      ""number_of_replicas"": 0
                    }
                  },
                  ""priority"": 500,
                  ""composed_of"": [],
                  ""_meta"": {
                    ""description"": ""GameGaraj log indices with ILM lifecycle management""
                  }
                }";

                var templateContent = new StringContent(templateJson, Encoding.UTF8, "application/json");
                var templateResponse = await client.PutAsync($"{_settings.ElasticSearchUri}/_index_template/gamegaraj-logs-template", templateContent);

                if (!templateResponse.IsSuccessStatusCode)
                {
                    var error = await templateResponse.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Index Template'ini oluştururken hata alındı: {error}";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Success"] = "Elasticsearch ILM Politikası ve İndeks Şablonu başarıyla senkronize edildi!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Elasticsearch bağlantı hatası: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }

    // ── Helper Models & DTOs ──

    public class ObservabilityDashboardViewModel
    {
        public List<ServiceObservabilityStatus> Services { get; set; } = new();
        public List<ElasticIndexDto> ElasticIndices { get; set; } = new();
        public bool IsIlmConfigured { get; set; }
    }

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

    public class ElasticIndexDto
    {
        public string Index { get; set; } = string.Empty;
        public string Health { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("docs.count")]
        public string DocsCount { get; set; } = "0";
        [System.Text.Json.Serialization.JsonPropertyName("store.size")]
        public string StoreSize { get; set; } = "0B";
    }
}
