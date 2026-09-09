using GameGaraj.Discussion.API.Dtos;

namespace GameGaraj.Discussion.API.Services;

public interface IDiscussionService
{
    // Public endpoints
    Task<QuestionListDto> GetProductQuestionsAsync(string productId, int page, int pageSize, string? currentUserId, CancellationToken cancellationToken);
    Task<List<QuestionDto>> GetMyQuestionsAsync(string userId, CancellationToken cancellationToken);
    Task<DiscussionMutationResultDto> CreateQuestionAsync(CreateQuestionDto dto, UserContext user, CancellationToken cancellationToken);
    Task<DiscussionMutationResultDto> DeleteQuestionAsync(string questionId, string userId, CancellationToken cancellationToken);
    Task<VoteResultDto> VoteAnswerAsync(string answerId, bool isHelpful, string userId, CancellationToken cancellationToken);

    // Admin endpoints
    Task<AdminQuestionListDto> GetAdminQuestionsAsync(int? status, bool? archived, string? query, int page, int pageSize, CancellationToken cancellationToken);
    Task<DiscussionMutationResultDto> AnswerQuestionAsync(string questionId, CreateAnswerDto dto, UserContext admin, CancellationToken cancellationToken);
    Task<DiscussionMutationResultDto> DeleteQuestionAsAdminAsync(string questionId, CancellationToken cancellationToken);
}
