namespace GameGaraj.Review.API.Dtos;

public class CreateReviewDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class UpdateReviewDto
{
    public string ReviewId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class ModerateReviewDto
{
    public string ReviewId { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? AdminNote { get; set; }
}

public class ToggleReviewReactionDto
{
    public string ReviewId { get; set; } = string.Empty;
    public bool IsLike { get; set; }
}

public class ReviewDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int Status { get; set; }
    public bool HasProfanity { get; set; }
    public bool HasPriceInfo { get; set; }
    public bool IsSpamSuspected { get; set; }
    public string? AdminNote { get; set; }
    public int LikeCount { get; set; }
    public int DislikeCount { get; set; }
    public string? CurrentUserReaction { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ReviewListDto
{
    public List<ReviewDto> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public double AverageRating { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new()
    {
        [1] = 0,
        [2] = 0,
        [3] = 0,
        [4] = 0,
        [5] = 0
    };
}

public class ProductReviewSummaryDto
{
    public string ProductId { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public double AverageRating { get; set; }
}

public class AdminReviewListDto
{
    public List<ReviewDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public int TotalPages { get; set; }
    public long PendingCount { get; set; }
    public long ApprovedCount { get; set; }
    public long RejectedCount { get; set; }
}

public class CanReviewDto
{
    public bool CanReview { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ReviewMutationResultDto
{
    public bool Succeeded { get; set; }
    public string? Id { get; set; }
    public bool HasProfanity { get; set; }
    public bool HasPriceInfo { get; set; }
    public bool IsSpamSuspected { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ReviewReactionResultDto
{
    public bool Succeeded { get; set; }
    public int LikeCount { get; set; }
    public int DislikeCount { get; set; }
    public string? CurrentUserReaction { get; set; }
}
