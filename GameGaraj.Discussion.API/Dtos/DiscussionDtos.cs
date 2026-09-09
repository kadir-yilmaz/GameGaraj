namespace GameGaraj.Discussion.API.Dtos;

// ─── Create DTOs ────────────────────────────────────────────────────

public class CreateQuestionDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
}

public class CreateAnswerDto
{
    public string AnswerText { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
}

public class VoteAnswerDto
{
    public bool IsHelpful { get; set; }
}

// ─── Response DTOs ──────────────────────────────────────────────────

public class QuestionDto
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
    public AnswerDto? Answer { get; set; }
}

public class AnswerDto
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

public class VoteResultDto
{
    public bool Succeeded { get; set; }
    public string AnswerId { get; set; } = string.Empty;
    public int HelpfulCount { get; set; }
    public int DislikeCount { get; set; }
    public string? UserVote { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class QuestionListDto
{
    public List<QuestionDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class AdminQuestionListDto
{
    public List<QuestionDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public int TotalPages { get; set; }
    public long PendingCount { get; set; }
    public long AnsweredCount { get; set; }
    public long ArchivedCount { get; set; }
}

public class DiscussionMutationResultDto
{
    public bool Succeeded { get; set; }
    public string? Id { get; set; }
    public string? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? UserId { get; set; }
    public bool IsEdit { get; set; }
    public bool HasProfanity { get; set; }
    public bool IsSpamSuspected { get; set; }
    public string Message { get; set; } = string.Empty;
}

// ─── User Context ───────────────────────────────────────────────────

public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
