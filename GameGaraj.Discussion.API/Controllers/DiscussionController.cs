using System.Security.Claims;
using GameGaraj.Discussion.API.Dtos;
using GameGaraj.Discussion.API.Hubs;
using GameGaraj.Discussion.API.Services;
using GameGaraj.Shared.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace GameGaraj.Discussion.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DiscussionController : ControllerBase
{
    private readonly IDiscussionService _discussionService;
    private readonly IHubContext<DiscussionHub> _hubContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public DiscussionController(
        IDiscussionService discussionService,
        IHubContext<DiscussionHub> hubContext,
        IPublishEndpoint publishEndpoint)
    {
        _discussionService = discussionService;
        _hubContext = hubContext;
        _publishEndpoint = publishEndpoint;
    }

    // ─── Public Endpoints ───────────────────────────────────────────

    /// <summary>
    /// Bir ürüne ait soruları listeler (anonim erişim).
    /// </summary>
    [HttpGet("product/{productId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProductQuestions(string productId, [FromQuery] int page = 0, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdOrNull();
        var result = await _discussionService.GetProductQuestionsAsync(productId, page, pageSize, userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Giriş yapmış kullanıcının kendi sorularını listeler.
    /// </summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyQuestions(CancellationToken cancellationToken)
    {
        if (!IsUserAuthenticated())
        {
            return Unauthorized(new DiscussionMutationResultDto { Succeeded = false, Message = "Giriş yapmanız gerekmektedir." });
        }

        var userId = GetRequiredUserId();
        var result = await _discussionService.GetMyQuestionsAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Yeni soru oluşturur. Soru önce DB'ye kaydedilir, sonra SignalR ile hem ürün sayfasına hem de admin paneline anlık bildirim gönderilir.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateQuestion(CreateQuestionDto dto, CancellationToken cancellationToken)
    {
        if (!IsUserAuthenticated())
        {
            return Unauthorized(new DiscussionMutationResultDto { Succeeded = false, Message = "Soru sormak için lütfen giriş yapın." });
        }

        var userContext = GetUserContext();
        var result = await _discussionService.CreateQuestionAsync(dto, userContext, cancellationToken);

        if (result.Succeeded && !string.IsNullOrEmpty(result.Id))
        {
            var eventPayload = new
            {
                Id = result.Id,
                ProductId = dto.ProductId,
                ProductName = dto.ProductName,
                ProductImageUrl = dto.ProductImageUrl,
                QuestionText = dto.QuestionText,
                UserName = userContext.UserName,
                FormattedUserName = FormatDisplayName(userContext.UserName),
                CreatedAt = DateTime.UtcNow,
                Status = 0,
                HasProfanity = result.HasProfanity,
                IsSpamSuspected = result.IsSpamSuspected
            };

            // Moderasyon Koruması: Sadece temiz sorular herkese açık ürün sayfasına anlık basılır
            if (!result.HasProfanity && !result.IsSpamSuspected)
            {
                await _hubContext.Clients.Group($"product-{dto.ProductId}")
                    .SendAsync("ReceiveNewQuestion", eventPayload, cancellationToken);

                // MassTransit ile RabbitMQ'ya publish (WebUI SignalR dağıtımı için)
                await _publishEndpoint.Publish(new QuestionCreated
                {
                    QuestionId = result.Id,
                    ProductId = dto.ProductId,
                    ProductName = dto.ProductName,
                    UserId = userContext.UserId,
                    UserName = userContext.UserName,
                    QuestionText = dto.QuestionText,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);
            }

            // Admin paneline her halükarda inceleme için anlık bildirim basılır
            await _hubContext.Clients.Group("admin-discussions")
                .SendAsync("ReceiveAdminNewQuestion", eventPayload, cancellationToken);
        }

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Satıcı cevabına Faydalı (Helpful) veya Faydalı Değil (Dislike) oyu verir/toggles.
    /// </summary>
    [HttpPost("answers/{answerId}/vote")]
    public async Task<IActionResult> VoteAnswer(string answerId, [FromBody] VoteAnswerDto dto, CancellationToken cancellationToken)
    {
        if (!IsUserAuthenticated())
        {
            return Unauthorized(new VoteResultDto { Succeeded = false, Message = "Oy kullanmak için lütfen giriş yapın." });
        }

        var userId = GetRequiredUserId();
        var result = await _discussionService.VoteAnswerAsync(answerId, dto.IsHelpful, userId, cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Kullanıcı kendi cevaplanmamış sorusunu siler.
    /// </summary>
    [HttpDelete("{questionId}")]
    public async Task<IActionResult> DeleteQuestion(string questionId, CancellationToken cancellationToken)
    {
        if (!IsUserAuthenticated())
        {
            return Unauthorized(new DiscussionMutationResultDto { Succeeded = false, Message = "Giriş yapmanız gerekmektedir." });
        }

        var userId = GetRequiredUserId();
        var result = await _discussionService.DeleteQuestionAsync(questionId, userId, cancellationToken);

        if (result.Succeeded)
        {
            var deletePayload = new
            {
                QuestionId = questionId,
                ProductId = result.ProductId
            };

            if (!string.IsNullOrEmpty(result.ProductId))
            {
                await _hubContext.Clients.Group($"product-{result.ProductId}")
                    .SendAsync("ReceiveQuestionDeleted", deletePayload, cancellationToken);
            }

            await _hubContext.Clients.Group("admin-discussions")
                .SendAsync("ReceiveAdminQuestionDeleted", deletePayload, cancellationToken);

            await _publishEndpoint.Publish(new QuestionDeleted
            {
                QuestionId = questionId,
                ProductId = result.ProductId ?? string.Empty,
                DeletedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    // ─── Admin Endpoints ────────────────────────────────────────────

    /// <summary>
    /// Admin: Tüm soruları filtreli listeler.
    /// </summary>
    [HttpGet("admin")]
    public async Task<IActionResult> GetAdminQuestions(
        [FromQuery] int? status = null,
        [FromQuery] bool? archived = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var result = await _discussionService.GetAdminQuestionsAsync(status, archived, q, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin: Soruya cevap verir. Cevap önce DB'ye kaydedilir, sonra SignalR ile anlık iletilir.
    /// </summary>
    [HttpPost("{questionId}/answer")]
    public async Task<IActionResult> AnswerQuestion(string questionId, CreateAnswerDto dto, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var adminContext = GetUserContext();
        var result = await _discussionService.AnswerQuestionAsync(questionId, dto, adminContext, cancellationToken);

        if (result.Succeeded && !string.IsNullOrEmpty(result.ProductId))
        {
            var answerPayload = new
            {
                QuestionId = questionId,
                AnswerId = result.Id,
                ProductId = result.ProductId,
                ProductName = result.ProductName ?? "Ürün",
                AnswerText = dto.AnswerText,
                ResponderName = adminContext.UserName,
                IsEdit = result.IsEdit,
                CreatedAt = DateTime.UtcNow
            };

            // 1. Ürün sayfasında bekleyen kullanıcılara satıcı cevabını anlık push
            await _hubContext.Clients.Group($"product-{result.ProductId}")
                .SendAsync("ReceiveQuestionAnswered", answerPayload, cancellationToken);

            // 2. Soruyu soran kullanıcıya (sitenin neresinde olursa olsun) özel push
            if (!string.IsNullOrEmpty(result.UserId))
            {
                await _hubContext.Clients.Group($"user-{result.UserId}")
                    .SendAsync("ReceiveMyQuestionAnswered", answerPayload, cancellationToken);
            }

            // 3. Admin paneline canlı güncellenme push
            await _hubContext.Clients.Group("admin-discussions")
                .SendAsync("ReceiveAdminQuestionAnswered", answerPayload, cancellationToken);

            // 4. RabbitMQ'ya publish
            await _publishEndpoint.Publish(new QuestionAnswered
            {
                QuestionId = questionId,
                AnswerId = result.Id ?? string.Empty,
                ProductId = result.ProductId,
                ProductName = result.ProductName ?? "Ürün",
                UserId = result.UserId ?? string.Empty,
                ResponderName = adminContext.UserName,
                AnswerText = dto.AnswerText,
                IsEdit = result.IsEdit,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Admin: Soruyu siler (soft delete).
    /// </summary>
    [HttpDelete("admin/{questionId}")]
    public async Task<IActionResult> DeleteQuestionAsAdmin(string questionId, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var result = await _discussionService.DeleteQuestionAsAdminAsync(questionId, cancellationToken);

        if (result.Succeeded)
        {
            var deletePayload = new
            {
                QuestionId = questionId,
                ProductId = result.ProductId
            };

            if (!string.IsNullOrEmpty(result.ProductId))
            {
                await _hubContext.Clients.Group($"product-{result.ProductId}")
                    .SendAsync("ReceiveQuestionDeleted", deletePayload, cancellationToken);
            }

            await _hubContext.Clients.Group("admin-discussions")
                .SendAsync("ReceiveAdminQuestionDeleted", deletePayload, cancellationToken);

            await _publishEndpoint.Publish(new QuestionDeleted
            {
                QuestionId = questionId,
                ProductId = result.ProductId ?? string.Empty,
                DeletedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private string? GetUserIdOrNull()
    {
        var id = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Request.Headers["X-User-Id"].FirstOrDefault();

        return (string.IsNullOrWhiteSpace(id) || id.StartsWith("guest-") || id == "anonymous-user") ? null : id;
    }

    private bool IsUserAuthenticated()
    {
        return !string.IsNullOrWhiteSpace(GetUserIdOrNull());
    }

    private bool IsAdmin()
    {
        if (User.IsInRole("admin")) return true;
        var roleHeader = Request.Headers["X-User-Role"].FirstOrDefault();
        if (!string.IsNullOrEmpty(roleHeader))
        {
            var roles = roleHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (roles.Any(r => string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }

    private string GetRequiredUserId()
    {
        var userId = GetUserIdOrNull();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("Giriş yapmanız gerekmektedir.");
        }

        return userId;
    }

    private UserContext GetUserContext()
    {
        var rawHeaderName = Request.Headers["X-User-Name"].FirstOrDefault();
        string? headerName = null;
        if (!string.IsNullOrEmpty(rawHeaderName))
        {
            try
            {
                headerName = Uri.UnescapeDataString(rawHeaderName);
            }
            catch
            {
                headerName = rawHeaderName;
            }
        }

        return new UserContext
        {
            UserId = GetRequiredUserId(),
            UserName = headerName
                ?? User.Identity?.Name
                ?? User.FindFirst("name")?.Value
                ?? User.FindFirst("preferred_username")?.Value
                ?? "Kullanıcı"
        };
    }

    private static string FormatDisplayName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "K. Yılmaz";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{parts[0][0].ToString().ToUpper()}. {string.Join(" ", parts.Skip(1))}";
        }
        return fullName;
    }
}
