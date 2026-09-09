using GameGaraj.WebUI.Models.Discussion;

namespace GameGaraj.WebUI.Services.Abstract;

public interface IDiscussionService
{
    // Public
    Task<QuestionListViewModel> GetProductQuestionsAsync(string productId, int page = 0, int pageSize = 10);
    Task<List<QuestionViewModel>> GetMyQuestionsAsync();
    Task<DiscussionMutationResultViewModel> CreateQuestionAsync(CreateQuestionInput input);
    Task<DiscussionMutationResultViewModel> DeleteQuestionAsync(string questionId);
    Task<VoteResultViewModel> VoteAnswerAsync(string answerId, bool isHelpful);

    // Admin
    Task<AdminQuestionListViewModel> GetAdminQuestionsAsync(int? status = null, bool? archived = null, string? query = null, int page = 1, int pageSize = 20);
    Task<DiscussionMutationResultViewModel> AnswerQuestionAsync(string questionId, string answerText);
    Task<DiscussionMutationResultViewModel> DeleteQuestionAsAdminAsync(string questionId);
}
