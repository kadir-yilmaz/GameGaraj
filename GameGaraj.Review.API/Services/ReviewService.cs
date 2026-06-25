using System.Diagnostics;
using GameGaraj.Review.API.Data;
using GameGaraj.Review.API.Dtos;
using GameGaraj.Review.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Review.API.Services;

public class ReviewService : IReviewService
{
    private readonly ReviewDbContext _context;
    private readonly IContentModerationService _contentModerationService;
    private readonly IOrderOwnershipClient _orderOwnershipClient;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        ReviewDbContext context,
        IContentModerationService contentModerationService,
        IOrderOwnershipClient orderOwnershipClient,
        ILogger<ReviewService> logger)
    {
        _context = context;
        _contentModerationService = contentModerationService;
        _orderOwnershipClient = orderOwnershipClient;
        _logger = logger;
    }

    public async Task<ReviewListDto> GetProductReviewsAsync(string productId, int page, int pageSize, string? currentUserId, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 0);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.ProductReviews
            .AsNoTracking()
            .Include(review => review.Reactions)
            .Where(review => review.ProductId == productId && review.Status == ReviewStatus.Approved);

        var totalCount = await query.CountAsync(cancellationToken);
        var distribution = await BuildDistributionAsync(query, cancellationToken);
        var average = totalCount > 0 ? await query.AverageAsync(review => review.Rating, cancellationToken) : 0;
        var reviewEntities = await query
            .OrderByDescending(review => review.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var reviews = reviewEntities.Select(review => MapReview(review, currentUserId)).ToList();

        return new ReviewListDto
        {
            Reviews = reviews,
            TotalCount = totalCount,
            AverageRating = average,
            RatingDistribution = distribution
        };
    }

    public async Task<Dictionary<string, ProductReviewSummaryDto>> GetProductReviewSummariesAsync(IReadOnlyCollection<string> productIds, CancellationToken cancellationToken)
    {
        var normalizedIds = productIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();

        if (normalizedIds.Count == 0)
        {
            return new Dictionary<string, ProductReviewSummaryDto>(StringComparer.OrdinalIgnoreCase);
        }

        var summaries = await _context.ProductReviews
            .AsNoTracking()
            .Where(review => normalizedIds.Contains(review.ProductId) && review.Status == ReviewStatus.Approved)
            .GroupBy(review => review.ProductId)
            .Select(group => new ProductReviewSummaryDto
            {
                ProductId = group.Key,
                TotalCount = group.Count(),
                AverageRating = group.Average(review => review.Rating)
            })
            .ToListAsync(cancellationToken);

        return summaries.ToDictionary(summary => summary.ProductId, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<AdminReviewListDto> GetAdminReviewsAsync(int? status, string? query, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var baseQuery = _context.ProductReviews
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(review => review.Reactions)
            .Where(review => !review.IsDeleted)
            .AsQueryable();

        var pendingCount = await baseQuery.LongCountAsync(review => review.Status == ReviewStatus.Pending, cancellationToken);
        var approvedCount = await baseQuery.LongCountAsync(review => review.Status == ReviewStatus.Approved, cancellationToken);
        var rejectedCount = await baseQuery.LongCountAsync(review => review.Status == ReviewStatus.Rejected, cancellationToken);

        var reviewsQuery = baseQuery;
        if (status.HasValue)
        {
            reviewsQuery = reviewsQuery.Where(review => review.Status == (ReviewStatus)status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.Trim().ToLower();
            reviewsQuery = reviewsQuery.Where(review =>
                review.ProductName.ToLower().Contains(normalized) ||
                review.UserName.ToLower().Contains(normalized) ||
                review.UserEmail.ToLower().Contains(normalized) ||
                review.Comment.ToLower().Contains(normalized));
        }

        var totalCount = await reviewsQuery.LongCountAsync(cancellationToken);
        var reviewEntities = await reviewsQuery
            .OrderByDescending(review => review.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = reviewEntities.Select(review => MapReview(review, null)).ToList();

        return new AdminReviewListDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
            PendingCount = pendingCount,
            ApprovedCount = approvedCount,
            RejectedCount = rejectedCount
        };
    }

    public async Task<List<ReviewDto>> GetUserReviewsAsync(string userId, CancellationToken cancellationToken)
    {
        var reviews = await _context.ProductReviews
            .AsNoTracking()
            .Include(review => review.Reactions)
            .Where(review => review.UserId == userId)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync(cancellationToken);

        return reviews.Select(review => MapReview(review, userId)).ToList();
    }

    public async Task<ReviewDto?> GetUserReviewAsync(string productId, string userId, CancellationToken cancellationToken)
    {
        var review = await _context.ProductReviews
            .AsNoTracking()
            .Include(review => review.Reactions)
            .Where(review => review.ProductId == productId && review.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return review == null ? null : MapReview(review, userId);
    }

    public async Task<CanReviewDto> CanReviewAsync(string productId, string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new CanReviewDto { CanReview = false, Reason = "Giris yapmaniz gerekiyor." };
        }

        var existing = await _context.ProductReviews
            .AnyAsync(review => review.ProductId == productId && review.UserId == userId, cancellationToken);

        if (existing)
        {
            return new CanReviewDto { CanReview = false, Reason = "Bu urun icin zaten yorumunuz var." };
        }

        var ownership = await _orderOwnershipClient.GetOwnershipAsync(userId, productId, cancellationToken);
        if (!ownership.Owns)
        {
            return new CanReviewDto { CanReview = false, Reason = "Yalnizca satin aldiginiz urunlere yorum yapabilirsiniz." };
        }

        return new CanReviewDto { CanReview = true };
    }

    public async Task<ReviewMutationResultDto> CreateAsync(CreateReviewDto dto, UserContext user, CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = Stopwatch.StartNew();

        var validation = Validate(dto.Rating, dto.Comment);
        _logger.LogInformation("Review create validation completed in {ElapsedMs} ms for user {UserId}, product {ProductId}", stepStopwatch.ElapsedMilliseconds, user.UserId, dto.ProductId);
        if (!string.IsNullOrEmpty(validation))
        {
            return new ReviewMutationResultDto { Succeeded = false, Message = validation };
        }

        stepStopwatch.Restart();
        var canReview = await CanReviewAsync(dto.ProductId, user.UserId, cancellationToken);
        _logger.LogInformation("Review create can-review check completed in {ElapsedMs} ms for user {UserId}, product {ProductId}, canReview {CanReview}", stepStopwatch.ElapsedMilliseconds, user.UserId, dto.ProductId, canReview.CanReview);
        if (!canReview.CanReview)
        {
            return new ReviewMutationResultDto { Succeeded = false, Message = canReview.Reason };
        }

        stepStopwatch.Restart();
        var recentComments = await _context.ProductReviews
            .AsNoTracking()
            .Where(review => review.UserId == user.UserId && review.CreatedAt >= DateTime.UtcNow.AddHours(-24))
            .OrderByDescending(review => review.CreatedAt)
            .Take(10)
            .Select(review => review.Comment)
            .ToListAsync(cancellationToken);
        _logger.LogInformation("Review create recent-comments query completed in {ElapsedMs} ms for user {UserId}", stepStopwatch.ElapsedMilliseconds, user.UserId);

        stepStopwatch.Restart();
        var analysis = _contentModerationService.Analyze(dto.Comment, recentComments);
        _logger.LogInformation("Review create moderation completed in {ElapsedMs} ms for user {UserId}, hasProfanity {HasProfanity}, hasPriceInfo {HasPriceInfo}, spam {IsSpamSuspected}", stepStopwatch.ElapsedMilliseconds, user.UserId, analysis.HasProfanity, analysis.HasPriceInfo, analysis.IsSpamSuspected);
        var requiresReview = analysis.HasProfanity || analysis.HasPriceInfo;
        var status = requiresReview ? ReviewStatus.Pending : ReviewStatus.Approved;
        var review = new ProductReview
        {
            Id = Guid.NewGuid().ToString(),
            ProductId = dto.ProductId,
            ProductName = dto.ProductName,
            ProductImageUrl = dto.ProductImageUrl,
            UserId = user.UserId,
            UserName = user.UserName,
            UserEmail = user.UserEmail,
            Rating = dto.Rating,
            Comment = dto.Comment.Trim(),
            Status = status,
            HasProfanity = analysis.HasProfanity,
            HasPriceInfo = analysis.HasPriceInfo,
            IsSpamSuspected = analysis.IsSpamSuspected,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductReviews.Add(review);
        stepStopwatch.Restart();
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Review create save completed in {ElapsedMs} ms for review {ReviewId}", stepStopwatch.ElapsedMilliseconds, review.Id);
        _logger.LogInformation("Review create completed in {ElapsedMs} ms for review {ReviewId}, status {Status}", totalStopwatch.ElapsedMilliseconds, review.Id, review.Status);

        return new ReviewMutationResultDto
        {
            Succeeded = true,
            Id = review.Id,
            HasProfanity = review.HasProfanity,
            HasPriceInfo = review.HasPriceInfo,
            IsSpamSuspected = review.IsSpamSuspected,
            Message = review.Status == ReviewStatus.Pending
                ? "Yorumunuz inceleme surecine alindi."
                : "Yorumunuz yayina alindi."
        };
    }

    public async Task<ReviewMutationResultDto> UpdateAsync(UpdateReviewDto dto, string userId, CancellationToken cancellationToken)
    {
        var validation = Validate(dto.Rating, dto.Comment);
        if (!string.IsNullOrEmpty(validation))
        {
            return new ReviewMutationResultDto { Succeeded = false, Message = validation };
        }

        var review = await _context.ProductReviews.FirstOrDefaultAsync(item => item.Id == dto.ReviewId, cancellationToken);
        if (review == null)
        {
            return new ReviewMutationResultDto { Succeeded = false, Message = "Yorum bulunamadi." };
        }

        if (review.UserId != userId)
        {
            return new ReviewMutationResultDto { Succeeded = false, Message = "Yalnizca kendi yorumunuzu duzenleyebilirsiniz." };
        }

        var recentComments = await _context.ProductReviews
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.Id != dto.ReviewId && item.CreatedAt >= DateTime.UtcNow.AddHours(-24))
            .Select(item => item.Comment)
            .ToListAsync(cancellationToken);

        var analysis = _contentModerationService.Analyze(dto.Comment, recentComments);
        review.Rating = dto.Rating;
        review.Comment = dto.Comment.Trim();
        var requiresReview = analysis.HasProfanity || analysis.HasPriceInfo;
        review.Status = requiresReview ? ReviewStatus.Pending : ReviewStatus.Approved;
        review.HasProfanity = analysis.HasProfanity;
        review.HasPriceInfo = analysis.HasPriceInfo;
        review.IsSpamSuspected = analysis.IsSpamSuspected;
        review.UpdatedAt = DateTime.UtcNow;
        review.AdminNote = null;

        await _context.SaveChangesAsync(cancellationToken);
        return new ReviewMutationResultDto
        {
            Succeeded = true,
            Id = review.Id,
            HasProfanity = review.HasProfanity,
            HasPriceInfo = review.HasPriceInfo,
            IsSpamSuspected = review.IsSpamSuspected,
            Message = review.Status == ReviewStatus.Pending
                ? "Yorumunuz guncellendi ve inceleme surecine alindi."
                : "Yorumunuz guncellendi ve yayina alindi."
        };
    }

    public async Task<ReviewMutationResultDto> DeleteAsync(string reviewId, string userId, CancellationToken cancellationToken)
    {
        var review = await _context.ProductReviews.FirstOrDefaultAsync(item => item.Id == reviewId, cancellationToken);
        if (review == null)
        {
            return new ReviewMutationResultDto { Succeeded = false, Message = "Yorum bulunamadi." };
        }

        if (review.UserId != userId)
        {
            return new ReviewMutationResultDto { Succeeded = false, Message = "Yalnizca kendi yorumunuzu silebilirsiniz." };
        }

        review.IsDeleted = true;
        review.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return new ReviewMutationResultDto { Succeeded = true, Message = "Yorum silindi." };
    }

    public async Task<ReviewMutationResultDto> DeleteAsAdminAsync(string reviewId, CancellationToken cancellationToken)
    {
        var review = await _context.ProductReviews
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == reviewId && !item.IsDeleted, cancellationToken);

        if (review == null)
        {
            return new ReviewMutationResultDto { Succeeded = false, Message = "Yorum bulunamadi." };
        }

        review.IsDeleted = true;
        review.DeletedAt = DateTime.UtcNow;
        review.AdminNote = "Admin tarafindan silindi.";
        review.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new ReviewMutationResultDto { Succeeded = true, Id = review.Id, Message = "Yorum silindi." };
    }

    public async Task<ReviewMutationResultDto> ModerateAsync(ModerateReviewDto dto, CancellationToken cancellationToken)
    {
        if (dto.Status != (int)ReviewStatus.Approved && dto.Status != (int)ReviewStatus.Rejected)
        {
            return new ReviewMutationResultDto { Succeeded = false, Message = "Gecersiz yorum durumu." };
        }

        var review = await _context.ProductReviews
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == dto.ReviewId && !item.IsDeleted, cancellationToken);

        if (review == null)
        {
            return new ReviewMutationResultDto { Succeeded = false, Message = "Yorum bulunamadi." };
        }

        review.Status = (ReviewStatus)dto.Status;
        review.AdminNote = dto.AdminNote;
        review.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new ReviewMutationResultDto { Succeeded = true, Id = review.Id, Message = "Yorum durumu guncellendi." };
    }

    public async Task<ReviewReactionResultDto> ToggleReactionAsync(ToggleReviewReactionDto dto, string userId, CancellationToken cancellationToken)
    {
        var reviewExists = await _context.ProductReviews.AnyAsync(review => review.Id == dto.ReviewId && review.Status == ReviewStatus.Approved, cancellationToken);
        if (!reviewExists)
        {
            return new ReviewReactionResultDto { Succeeded = false };
        }

        var reaction = await _context.ProductReviewReactions
            .FirstOrDefaultAsync(item => item.ReviewId == dto.ReviewId && item.UserId == userId, cancellationToken);

        if (reaction == null)
        {
            _context.ProductReviewReactions.Add(new ProductReviewReaction
            {
                Id = Guid.NewGuid().ToString(),
                ReviewId = dto.ReviewId,
                UserId = userId,
                IsLike = dto.IsLike,
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (reaction.IsLike == dto.IsLike)
        {
            _context.ProductReviewReactions.Remove(reaction);
        }
        else
        {
            reaction.IsLike = dto.IsLike;
            reaction.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await BuildReactionResultAsync(dto.ReviewId, userId, cancellationToken);
    }

    private static string Validate(int rating, string comment)
    {
        if (rating is < 1 or > 5)
        {
            return "Puan 1 ile 5 arasinda olmalidir.";
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            return "Yorum metni bos olamaz.";
        }

        var trimmed = comment.Trim();
        if (trimmed.Length < 10)
        {
            return "Yorum en az 10 karakter olmalidir.";
        }

        if (trimmed.Length > 1000)
        {
            return "Yorum en fazla 1000 karakter olabilir.";
        }

        return string.Empty;
    }

    private async Task<ReviewReactionResultDto> BuildReactionResultAsync(string reviewId, string userId, CancellationToken cancellationToken)
    {
        var reactions = _context.ProductReviewReactions.AsNoTracking().Where(item => item.ReviewId == reviewId);
        var currentUserReaction = await reactions
            .Where(item => item.UserId == userId)
            .Select(item => item.IsLike ? "like" : "dislike")
            .FirstOrDefaultAsync(cancellationToken);

        return new ReviewReactionResultDto
        {
            Succeeded = true,
            LikeCount = await reactions.CountAsync(item => item.IsLike, cancellationToken),
            DislikeCount = await reactions.CountAsync(item => !item.IsLike, cancellationToken),
            CurrentUserReaction = currentUserReaction
        };
    }

    private static async Task<Dictionary<int, int>> BuildDistributionAsync(IQueryable<ProductReview> query, CancellationToken cancellationToken)
    {
        var distribution = new Dictionary<int, int>
        {
            [1] = 0,
            [2] = 0,
            [3] = 0,
            [4] = 0,
            [5] = 0
        };

        var groups = await query
            .GroupBy(review => review.Rating)
            .Select(group => new { Rating = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        foreach (var group in groups)
        {
            distribution[group.Rating] = group.Count;
        }

        return distribution;
    }

    private static ReviewDto MapReview(ProductReview review, string? currentUserId)
    {
        return new ReviewDto
        {
            Id = review.Id,
            ProductId = review.ProductId,
            ProductName = review.ProductName,
            ProductImageUrl = review.ProductImageUrl,
            UserId = review.UserId,
            UserName = string.IsNullOrWhiteSpace(review.UserName) ? "Anonim" : review.UserName,
            UserEmail = review.UserEmail,
            Rating = review.Rating,
            Comment = review.Comment,
            Status = (int)review.Status,
            HasProfanity = review.HasProfanity,
            HasPriceInfo = review.HasPriceInfo,
            IsSpamSuspected = review.IsSpamSuspected,
            AdminNote = review.AdminNote,
            LikeCount = review.Reactions.Count(reaction => reaction.IsLike),
            DislikeCount = review.Reactions.Count(reaction => !reaction.IsLike),
            CurrentUserReaction = currentUserId == null
                ? null
                : review.Reactions
                    .Where(reaction => reaction.UserId == currentUserId)
                    .Select(reaction => reaction.IsLike ? "like" : "dislike")
                    .FirstOrDefault(),
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };
    }
}
