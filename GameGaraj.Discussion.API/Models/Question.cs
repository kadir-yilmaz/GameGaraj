namespace GameGaraj.Discussion.API.Models;

public class Question
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public QuestionStatus Status { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    public bool HasProfanity { get; set; }
    public bool IsSpamSuspected { get; set; }
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Answer? Answer { get; set; }
}
