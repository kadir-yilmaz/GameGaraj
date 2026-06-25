using GameGaraj.Review.API.Dtos;

namespace GameGaraj.Review.API.Services;

public interface IReviewService
{
    Task<ReviewListDto> GetProductReviewsAsync(string productId, int page, int pageSize, string? currentUserId, CancellationToken cancellationToken);
    Task<Dictionary<string, ProductReviewSummaryDto>> GetProductReviewSummariesAsync(IReadOnlyCollection<string> productIds, CancellationToken cancellationToken);
    Task<AdminReviewListDto> GetAdminReviewsAsync(int? status, string? query, int page, int pageSize, CancellationToken cancellationToken);
    Task<List<ReviewDto>> GetUserReviewsAsync(string userId, CancellationToken cancellationToken);
    Task<ReviewDto?> GetUserReviewAsync(string productId, string userId, CancellationToken cancellationToken);
    Task<CanReviewDto> CanReviewAsync(string productId, string userId, CancellationToken cancellationToken);
    Task<ReviewMutationResultDto> CreateAsync(CreateReviewDto dto, UserContext user, CancellationToken cancellationToken);
    Task<ReviewMutationResultDto> UpdateAsync(UpdateReviewDto dto, string userId, CancellationToken cancellationToken);
    Task<ReviewMutationResultDto> DeleteAsync(string reviewId, string userId, CancellationToken cancellationToken);
    Task<ReviewMutationResultDto> ModerateAsync(ModerateReviewDto dto, CancellationToken cancellationToken);
    Task<ReviewReactionResultDto> ToggleReactionAsync(ToggleReviewReactionDto dto, string userId, CancellationToken cancellationToken);
}

public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
}
