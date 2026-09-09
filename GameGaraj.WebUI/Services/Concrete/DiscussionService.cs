using System.Text;
using System.Text.Json;
using GameGaraj.WebUI.Models.Discussion;
using GameGaraj.WebUI.Services.Abstract;

namespace GameGaraj.WebUI.Services.Concrete;

public class DiscussionService : IDiscussionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscussionService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DiscussionService(HttpClient httpClient, ILogger<DiscussionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // ─── Public ─────────────────────────────────────────────────────

    public async Task<QuestionListViewModel> GetProductQuestionsAsync(string productId, int page = 0, int pageSize = 10)
    {
        try
        {
            var response = await _httpClient.GetAsync($"discussion/product/{Uri.EscapeDataString(productId)}?page={page}&pageSize={pageSize}");
            return await ReadOrDefaultAsync(response, new QuestionListViewModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DiscussionService] Product questions could not be loaded for {ProductId}", productId);
            return new QuestionListViewModel();
        }
    }

    public async Task<List<QuestionViewModel>> GetMyQuestionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("discussion/my");
            return await ReadOrDefaultAsync(response, new List<QuestionViewModel>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DiscussionService] My questions could not be loaded");
            return new List<QuestionViewModel>();
        }
    }

    public async Task<DiscussionMutationResultViewModel> CreateQuestionAsync(CreateQuestionInput input)
    {
        try
        {
            var json = JsonSerializer.Serialize(input);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("discussion", content);
            return await ReadMutationResultAsync(response, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DiscussionService] Question could not be created for product {ProductId}", input.ProductId);
            return new DiscussionMutationResultViewModel { Succeeded = false, Message = "Bir hata oluştu: " + ex.Message };
        }
    }

    public async Task<DiscussionMutationResultViewModel> DeleteQuestionAsync(string questionId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"discussion/{Uri.EscapeDataString(questionId)}");
            return await ReadMutationResultAsync(response, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DiscussionService] Question {QuestionId} could not be deleted", questionId);
            return new DiscussionMutationResultViewModel { Succeeded = false, Message = "Bir hata oluştu." };
        }
    }

    public async Task<VoteResultViewModel> VoteAnswerAsync(string answerId, bool isHelpful)
    {
        try
        {
            var payload = new { IsHelpful = isHelpful };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"discussion/answers/{Uri.EscapeDataString(answerId)}/vote", content);
            var json = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<VoteResultViewModel>(json, JsonOptions) ?? new VoteResultViewModel { Succeeded = true };
            }

            return new VoteResultViewModel { Succeeded = false, Message = "Oylama gerçekleştirilemedi." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DiscussionService] Vote failed for answer {AnswerId}", answerId);
            return new VoteResultViewModel { Succeeded = false, Message = "Bir hata oluştu." };
        }
    }

    // ─── Admin ──────────────────────────────────────────────────────

    public async Task<AdminQuestionListViewModel> GetAdminQuestionsAsync(int? status = null, bool? archived = null, string? query = null, int page = 1, int pageSize = 20)
    {
        try
        {
            var url = $"discussion/admin?page={page}&pageSize={pageSize}";
            if (status.HasValue) url += $"&status={status.Value}";
            if (archived.HasValue) url += $"&archived={archived.Value}";
            if (!string.IsNullOrWhiteSpace(query)) url += $"&q={Uri.EscapeDataString(query)}";

            var response = await _httpClient.GetAsync(url);
            return await ReadOrDefaultAsync(response, new AdminQuestionListViewModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DiscussionService] Admin questions could not be loaded");
            return new AdminQuestionListViewModel();
        }
    }

    public async Task<DiscussionMutationResultViewModel> AnswerQuestionAsync(string questionId, string answerText)
    {
        try
        {
            var payload = new { AnswerText = answerText };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"discussion/{Uri.EscapeDataString(questionId)}/answer", content);
            return await ReadMutationResultAsync(response, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DiscussionService] Answer could not be sent for question {QuestionId}", questionId);
            return new DiscussionMutationResultViewModel { Succeeded = false, Message = "Bir hata oluştu." };
        }
    }

    public async Task<DiscussionMutationResultViewModel> DeleteQuestionAsAdminAsync(string questionId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"discussion/admin/{Uri.EscapeDataString(questionId)}");
            return await ReadMutationResultAsync(response, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DiscussionService] Question {QuestionId} could not be deleted", questionId);
            return new DiscussionMutationResultViewModel { Succeeded = false, Message = "Bir hata oluştu." };
        }
    }

    private static async Task<T> ReadOrDefaultAsync<T>(HttpResponseMessage response, T defaultValue) where T : class
    {
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private static async Task<DiscussionMutationResultViewModel> ReadMutationResultAsync(HttpResponseMessage response, ILogger logger)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[DiscussionService] Mutation failed with {StatusCode}. Response: {Content}", response.StatusCode, content);
        }

        DiscussionMutationResultViewModel? result = null;
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                result = JsonSerializer.Deserialize<DiscussionMutationResultViewModel>(content, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "[DiscussionService] Mutation response could not be parsed: {Content}", content);
            }
        }

        return result ?? new DiscussionMutationResultViewModel
        {
            Succeeded = response.IsSuccessStatusCode,
            Message = response.IsSuccessStatusCode ? "İşlem başarıyla tamamlandı." : $"İşlem gerçekleştirilemedi ({(int)response.StatusCode})."
        };
    }
}
