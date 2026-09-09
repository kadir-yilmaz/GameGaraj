namespace GameGaraj.Discussion.API.Models;

public class AnswerVote
{
    public string Id { get; set; } = string.Empty;
    public string AnswerId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool IsHelpful { get; set; } // true: Helpful (Upvote), false: Dislike (Downvote)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Answer Answer { get; set; } = null!;
}
