using System.Security.Claims;
using GameGaraj.Review.API.Dtos;
using GameGaraj.Review.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Review.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet("product/{productId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProductReviews(string productId, [FromQuery] int page = 0, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdOrNull();
        var result = await _reviewService.GetProductReviewsAsync(productId, page, pageSize, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("summaries")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProductReviewSummaries([FromBody] List<string> productIds, CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetProductReviewSummariesAsync(productIds, cancellationToken);
        return Ok(result);
    }

    [HttpGet("user/{productId}")]
    [Authorize]
    public async Task<IActionResult> GetUserReview(string productId, CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        var result = await _reviewService.GetUserReviewAsync(productId, userId, cancellationToken);
        return Ok(new { HasReview = result != null, Review = result });
    }

    [HttpGet("my-reviews")]
    [Authorize]
    public async Task<IActionResult> GetMyReviews(CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        var result = await _reviewService.GetUserReviewsAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("can-review/{productId}")]
    [Authorize]
    public async Task<IActionResult> CanReview(string productId, CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        var result = await _reviewService.CanReviewAsync(productId, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreateReviewDto dto, CancellationToken cancellationToken)
    {
        var result = await _reviewService.CreateAsync(dto, GetUserContext(), cancellationToken);
        return result.Succeeded ? CreatedAtAction(nameof(GetUserReview), new { productId = dto.ProductId }, result) : BadRequest(result);
    }

    [HttpPut]
    [Authorize]
    public async Task<IActionResult> Update(UpdateReviewDto dto, CancellationToken cancellationToken)
    {
        var result = await _reviewService.UpdateAsync(dto, GetRequiredUserId(), cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var result = await _reviewService.DeleteAsync(id, GetRequiredUserId(), cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpPost("react")]
    [Authorize]
    public async Task<IActionResult> ToggleReaction(ToggleReviewReactionDto dto, CancellationToken cancellationToken)
    {
        var result = await _reviewService.ToggleReactionAsync(dto, GetRequiredUserId(), cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpGet("admin")]
    [Authorize(Roles = "admin, editor")]
    public async Task<IActionResult> GetAdminReviews([FromQuery] int? status = null, [FromQuery] string? q = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _reviewService.GetAdminReviewsAsync(status, q, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPut("admin/moderate")]
    [Authorize(Roles = "admin, editor")]
    public async Task<IActionResult> Moderate(ModerateReviewDto dto, CancellationToken cancellationToken)
    {
        var result = await _reviewService.ModerateAsync(dto, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    private string? GetUserIdOrNull()
    {
        return User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Request.Headers["X-User-Id"].FirstOrDefault();
    }

    private string GetRequiredUserId()
    {
        var userId = GetUserIdOrNull();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("User id was not found.");
        }

        return userId;
    }

    private UserContext GetUserContext()
    {
        return new UserContext
        {
            UserId = GetRequiredUserId(),
            UserName = User.Identity?.Name
                ?? User.FindFirst("preferred_username")?.Value
                ?? User.FindFirst("name")?.Value
                ?? "User",
            UserEmail = User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value
                ?? Request.Headers["X-User-Email"].FirstOrDefault()
                ?? string.Empty
        };
    }
}
