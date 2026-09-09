namespace GameGaraj.Discussion.API.Models;

public class Answer
{
    public string Id { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string ResponderId { get; set; } = string.Empty;
    public string ResponderName { get; set; } = string.Empty;
    public string AnswerText { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public int HelpfulCount { get; set; } = 0;
    public int DislikeCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; }

    public Question Question { get; set; } = null!;
    public ICollection<AnswerVote> Votes { get; set; } = new List<AnswerVote>();
}
