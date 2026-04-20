using GameGaraj.WebUI.Models.Campaigns;
using GameGaraj.WebUI.Services.Abstract;
using System.Text;
using System.Text.Json;

namespace GameGaraj.WebUI.Services.Concrete
{
    /// <summary>
    /// Campaign API'ye HTTP istekleri gönderen servis implementasyonu.
    /// </summary>
    public class CampaignService : ICampaignService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CampaignService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public CampaignService(HttpClient httpClient, ILogger<CampaignService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        // ───── CRUD ─────

        public async Task<List<CampaignRuleViewModel>> GetAllRulesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("campaignrules");
                if (!response.IsSuccessStatusCode) return new();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<CampaignRuleViewModel>>(content, _jsonOptions) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetAllRulesAsync hatası");
                return new();
            }
        }

        public async Task<CampaignRuleViewModel?> GetRuleByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"campaignrules/{id}");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CampaignRuleViewModel>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetRuleByIdAsync hatası — ID: {Id}", id);
                return null;
            }
        }

        public async Task<bool> CreateRuleAsync(CampaignRuleCreateInput input)
        {
            try
            {
                var json = JsonSerializer.Serialize(input);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("campaignrules", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] CreateRuleAsync hatası");
                return false;
            }
        }

        public async Task<bool> UpdateRuleAsync(CampaignRuleUpdateInput input)
        {
            try
            {
                var json = JsonSerializer.Serialize(input);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync("campaignrules", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] UpdateRuleAsync hatası");
                return false;
            }
        }

        public async Task<bool> DeleteRuleAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"campaignrules/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] DeleteRuleAsync hatası — ID: {Id}", id);
                return false;
            }
        }

        // ───── Hesaplama ─────

        public async Task<CalculateDiscountResponse?> CalculateDiscountAsync(CalculateDiscountRequest request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("campaigncalculate", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[CampaignService] CalculateDiscountAsync — HTTP {StatusCode}", response.StatusCode);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CalculateDiscountResponse>(responseContent, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] CalculateDiscountAsync hatası");
                return null;
            }
        }

        // ───── Kargo Ayarları ─────
        public async Task<ShippingSettingViewModel?> GetShippingSettingAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("shippingsettings");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ShippingSettingViewModel>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetShippingSettingAsync hatası");
                return null;
            }
        }

        public async Task<bool> UpdateShippingSettingAsync(ShippingSettingViewModel input)
        {
            try
            {
                var json = JsonSerializer.Serialize(input);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync("shippingsettings", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] UpdateShippingSettingAsync hatası");
                return false;
            }
        }
    }
}
