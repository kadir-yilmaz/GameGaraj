namespace GameGaraj.Review.API.Models;

public class ProductReviewReaction
{
    public string Id { get; set; } = string.Empty;
    public string ReviewId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool IsLike { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ProductReview Review { get; set; } = null!;
}
