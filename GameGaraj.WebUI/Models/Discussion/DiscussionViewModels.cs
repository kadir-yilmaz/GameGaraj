namespace GameGaraj.WebUI.Models.Discussion;

// ─── Input Models ───────────────────────────────────────────────────

public class CreateQuestionInput
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
}

public class AnswerQuestionInput
{
    public string QuestionId { get; set; } = string.Empty;
    public string AnswerText { get; set; } = string.Empty;
}

public class VoteAnswerInput
{
    public string AnswerId { get; set; } = string.Empty;
    public bool IsHelpful { get; set; }
}

// ─── View Models ────────────────────────────────────────────────────

public class QuestionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public int Status { get; set; }
    public bool IsArchived { get; set; }
    public bool HasProfanity { get; set; }
    public bool IsSpamSuspected { get; set; }
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public AnswerViewModel? Answer { get; set; }
}

public class AnswerViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ResponderId { get; set; } = string.Empty;
    public string ResponderName { get; set; } = string.Empty;
    public string AnswerText { get; set; } = string.Empty;
    public int HelpfulCount { get; set; }
    public int DislikeCount { get; set; }
    public string? UserVote { get; set; } // "helpful", "dislike" or null
    public DateTime CreatedAt { get; set; }
}

public class VoteResultViewModel
{
    public bool Succeeded { get; set; }
    public string AnswerId { get; set; } = string.Empty;
    public int HelpfulCount { get; set; }
    public int DislikeCount { get; set; }
    public string? UserVote { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class QuestionListViewModel
{
    public List<QuestionViewModel> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class AdminQuestionListViewModel
{
    public List<QuestionViewModel> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public int TotalPages { get; set; }
    public long PendingCount { get; set; }
    public long AnsweredCount { get; set; }
    public long ArchivedCount { get; set; }
}

public class DiscussionMutationResultViewModel
{
    public bool Succeeded { get; set; }
    public string? Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
