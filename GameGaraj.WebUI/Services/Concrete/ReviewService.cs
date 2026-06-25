using System.Text.Json;
using GameGaraj.WebUI.Models.Reviews;
using GameGaraj.WebUI.Services.Abstract;

namespace GameGaraj.WebUI.Services.Concrete;

public class ReviewService : IReviewService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReviewService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ReviewService(HttpClient httpClient, ILogger<ReviewService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ReviewListViewModel> GetProductReviewsAsync(string productId, int page = 0, int pageSize = 10)
    {
        try
        {
            var response = await _httpClient.GetAsync($"reviews/product/{Uri.EscapeDataString(productId)}?page={page}&pageSize={pageSize}");
            return await ReadOrDefaultAsync(response, new ReviewListViewModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReviewService] Product reviews could not be loaded for {ProductId}", productId);
            return new ReviewListViewModel();
        }
    }

    public async Task<Dictionary<string, ProductReviewSummaryViewModel>> GetProductReviewSummariesAsync(IEnumerable<string> productIds)
    {
        var ids = productIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<string, ProductReviewSummaryViewModel>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync("reviews/summaries", ids);
            return await ReadOrDefaultAsync(response, new Dictionary<string, ProductReviewSummaryViewModel>(StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReviewService] Product review summaries could not be loaded.");
            return new Dictionary<string, ProductReviewSummaryViewModel>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<CanReviewViewModel> CanReviewAsync(string productId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"reviews/can-review/{Uri.EscapeDataString(productId)}");
            return await ReadOrDefaultAsync(response, new CanReviewViewModel());
        }
        catch
        {
            return new CanReviewViewModel();
        }
    }

    public async Task<UserReviewResponseViewModel> GetUserReviewAsync(string productId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"reviews/user/{Uri.EscapeDataString(productId)}");
            return await ReadOrDefaultAsync(response, new UserReviewResponseViewModel());
        }
        catch
        {
            return new UserReviewResponseViewModel();
        }
    }

    public async Task<List<ReviewViewModel>> GetMyReviewsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("reviews/my-reviews");
            return await ReadOrDefaultAsync(response, new List<ReviewViewModel>());
        }
        catch
        {
            return new List<ReviewViewModel>();
        }
    }

    public async Task<ReviewMutationResultViewModel> CreateAsync(CreateReviewInput input)
    {
        var response = await _httpClient.PostAsJsonAsync("reviews", input);
        return await ReadMutationResultAsync(response, _logger);
    }

    public async Task<ReviewMutationResultViewModel> UpdateAsync(UpdateReviewInput input)
    {
        var response = await _httpClient.PutAsJsonAsync("reviews", input);
        return await ReadMutationResultAsync(response, _logger);
    }

    public async Task<ReviewMutationResultViewModel> DeleteAsync(string reviewId)
    {
        var response = await _httpClient.DeleteAsync($"reviews/{Uri.EscapeDataString(reviewId)}");
        return await ReadMutationResultAsync(response, _logger);
    }

    public async Task<AdminReviewListViewModel> GetAdminReviewsAsync(int? status = null, string? query = null, int page = 1, int pageSize = 20)
    {
        var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (status.HasValue) queryParams.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(query)) queryParams.Add($"q={Uri.EscapeDataString(query)}");

        var response = await _httpClient.GetAsync($"reviews/admin?{string.Join("&", queryParams)}");
        return await ReadOrDefaultAsync(response, new AdminReviewListViewModel { Page = page, PageSize = pageSize });
    }

    public async Task<ReviewMutationResultViewModel> ModerateAsync(ModerateReviewInput input)
    {
        var response = await _httpClient.PutAsJsonAsync("reviews/admin/moderate", input);
        return await ReadMutationResultAsync(response, _logger);
    }

    private static async Task<T> ReadOrDefaultAsync<T>(HttpResponseMessage response, T defaultValue)
    {
        if (!response.IsSuccessStatusCode)
        {
            return defaultValue;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions) ?? defaultValue;
    }

    private static async Task<ReviewMutationResultViewModel> ReadMutationResultAsync(HttpResponseMessage response, ILogger logger)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[ReviewService] Mutation failed with {StatusCode}. Response: {Content}", response.StatusCode, content);
        }

        ReviewMutationResultViewModel? result = null;
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                result = JsonSerializer.Deserialize<ReviewMutationResultViewModel>(content, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "[ReviewService] Mutation response could not be parsed.");
            }
        }

        return result ?? new ReviewMutationResultViewModel
        {
            Succeeded = response.IsSuccessStatusCode,
            Message = response.IsSuccessStatusCode ? "Islem tamamlandi." : "Islem tamamlanamadi."
        };
    }
}
