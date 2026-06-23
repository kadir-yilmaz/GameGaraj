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

        // ───── KUPON YÖNETİMİ ─────

        public async Task<List<CouponViewModel>> GetAllCouponsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("coupons");
                if (!response.IsSuccessStatusCode) return new();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<CouponViewModel>>(content, _jsonOptions) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetAllCouponsAsync hatası");
                return new();
            }
        }

        public async Task<CouponViewModel?> GetCouponByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"coupons/{id}");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CouponViewModel>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetCouponByIdAsync hatası — ID: {Id}", id);
                return null;
            }
        }

        public async Task<CouponViewModel?> GetCouponByCodeAsync(string code)
        {
            try
            {
                var response = await _httpClient.GetAsync($"coupons/code/{code}");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CouponViewModel>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetCouponByCodeAsync hatası — Code: {Code}", code);
                return null;
            }
        }

        public async Task<List<CouponViewModel>> GetPublicCouponsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("coupons/public");
                if (!response.IsSuccessStatusCode) return new();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<CouponViewModel>>(content, _jsonOptions) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetPublicCouponsAsync hatası");
                return new();
            }
        }

        public async Task<List<CouponViewModel>> GetUserCouponsAsync(string userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"coupons/user/{userId}");
                if (!response.IsSuccessStatusCode) return new();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<CouponViewModel>>(content, _jsonOptions) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetUserCouponsAsync hatası — User: {UserId}", userId);
                return new();
            }
        }

        public async Task<bool> CreateCouponAsync(CouponCreateInput input)
        {
            try
            {
                var json = JsonSerializer.Serialize(input);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("coupons", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] CreateCouponAsync hatası");
                return false;
            }
        }

        public async Task<bool> DeleteCouponAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"coupons/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] DeleteCouponAsync hatası — ID: {Id}", id);
                return false;
            }
        }

        public async Task<bool> MarkCouponAsUsedAsync(string code)
        {
            try
            {
                var response = await _httpClient.PutAsync($"coupons/use/{code}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] MarkCouponAsUsedAsync hatası — Code: {Code}", code);
                return false;
            }
        }

        // ───── KUPON KAZAN KURALLARI ─────

        public async Task<List<CouponRewardRuleViewModel>> GetAllRewardRulesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("couponrewardrules");
                if (!response.IsSuccessStatusCode) return new();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<CouponRewardRuleViewModel>>(content, _jsonOptions) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetAllRewardRulesAsync hatası");
                return new();
            }
        }

        public async Task<CouponRewardRuleViewModel?> GetRewardRuleByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"couponrewardrules/{id}");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CouponRewardRuleViewModel>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetRewardRuleByIdAsync hatası — ID: {Id}", id);
                return null;
            }
        }

        public async Task<bool> CreateRewardRuleAsync(CouponRewardRuleCreateInput input)
        {
            try
            {
                var json = JsonSerializer.Serialize(input);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("couponrewardrules", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] CreateRewardRuleAsync hatası");
                return false;
            }
        }

        public async Task<bool> UpdateRewardRuleAsync(CouponRewardRuleViewModel input)
        {
            try
            {
                var json = JsonSerializer.Serialize(input);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync("couponrewardrules", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] UpdateRewardRuleAsync hatası");
                return false;
            }
        }

        public async Task<bool> DeleteRewardRuleAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"couponrewardrules/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] DeleteRewardRuleAsync hatası — ID: {Id}", id);
                return false;
            }
        }

        // ───── BİLDİRİMLER ─────

        public async Task<List<NotificationViewModel>> GetNotificationsAsync(string userId, bool unreadOnly = false)
        {
            try
            {
                var response = await _httpClient.GetAsync($"notifications/user/{userId}?unreadOnly={unreadOnly}");
                if (!response.IsSuccessStatusCode) return new();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<NotificationViewModel>>(content, _jsonOptions) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetNotificationsAsync hatası — User: {UserId}", userId);
                return new();
            }
        }

        public async Task<int> GetUnreadNotificationCountAsync(string userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"notifications/unread-count/{userId}");
                if (!response.IsSuccessStatusCode) return 0;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<int>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetUnreadNotificationCountAsync hatası — User: {UserId}", userId);
                return 0;
            }
        }

        public async Task<bool> MarkNotificationAsReadAsync(int id)
        {
            try
            {
                var response = await _httpClient.PutAsync($"notifications/read/{id}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] MarkNotificationAsReadAsync hatası — ID: {Id}", id);
                return false;
            }
        }

        // ───── CAROUSEL IMAGES ─────

        public async Task<List<CarouselImageViewModel>> GetCarouselImagesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("carouselimages");
                if (!response.IsSuccessStatusCode) return new();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<CarouselImageViewModel>>(content, _jsonOptions) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] GetCarouselImagesAsync hatası");
                return new();
            }
        }

        public async Task<bool> CreateCarouselImageAsync(CarouselImageViewModel input)
        {
            try
            {
                var json = JsonSerializer.Serialize(input);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("carouselimages", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] CreateCarouselImageAsync hatası");
                return false;
            }
        }

        public async Task<bool> DeleteCarouselImageAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"carouselimages/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CampaignService] DeleteCarouselImageAsync hatası — ID: {Id}", id);
                return false;
            }
        }
    }
}
