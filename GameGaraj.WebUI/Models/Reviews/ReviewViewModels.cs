namespace GameGaraj.WebUI.Models.Reviews;

public class CreateReviewInput
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class UpdateReviewInput
{
    public string ReviewId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class ModerateReviewInput
{
    public string ReviewId { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? AdminNote { get; set; }
}

public class ReviewViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
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

public class ReviewListViewModel
{
    public List<ReviewViewModel> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public double AverageRating { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
}

public class ProductReviewSummaryViewModel
{
    public string ProductId { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public double AverageRating { get; set; }
}

public class AdminReviewListViewModel
{
    public List<ReviewViewModel> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public int TotalPages { get; set; }
    public long PendingCount { get; set; }
    public long ApprovedCount { get; set; }
    public long RejectedCount { get; set; }
}

public class CanReviewViewModel
{
    public bool CanReview { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class UserReviewResponseViewModel
{
    public bool HasReview { get; set; }
    public ReviewViewModel? Review { get; set; }
}

public class ReviewMutationResultViewModel
{
    public bool Succeeded { get; set; }
    public string? Id { get; set; }
    public bool HasProfanity { get; set; }
    public bool HasPriceInfo { get; set; }
    public bool IsSpamSuspected { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ReviewableProductViewModel
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
}
