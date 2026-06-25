using GameGaraj.WebUI.Models.Reviews;

namespace GameGaraj.WebUI.Services.Abstract;

public interface IReviewService
{
    Task<ReviewListViewModel> GetProductReviewsAsync(string productId, int page = 0, int pageSize = 10);
    Task<Dictionary<string, ProductReviewSummaryViewModel>> GetProductReviewSummariesAsync(IEnumerable<string> productIds);
    Task<CanReviewViewModel> CanReviewAsync(string productId);
    Task<UserReviewResponseViewModel> GetUserReviewAsync(string productId);
    Task<List<ReviewViewModel>> GetMyReviewsAsync();
    Task<ReviewMutationResultViewModel> CreateAsync(CreateReviewInput input);
    Task<ReviewMutationResultViewModel> UpdateAsync(UpdateReviewInput input);
    Task<ReviewMutationResultViewModel> DeleteAsync(string reviewId);
    Task<AdminReviewListViewModel> GetAdminReviewsAsync(int? status = null, string? query = null, int page = 1, int pageSize = 20);
    Task<ReviewMutationResultViewModel> ModerateAsync(ModerateReviewInput input);
    Task<ReviewMutationResultViewModel> DeleteAsAdminAsync(string reviewId);
}
