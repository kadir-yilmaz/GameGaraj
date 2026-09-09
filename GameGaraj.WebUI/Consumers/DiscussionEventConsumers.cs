using GameGaraj.Shared.Events;
using GameGaraj.WebUI.Hubs;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace GameGaraj.WebUI.Consumers;

public class QuestionCreatedWebConsumer : IConsumer<QuestionCreated>
{
    private readonly IHubContext<DiscussionHub> _hubContext;
    private readonly ILogger<QuestionCreatedWebConsumer> _logger;

    public QuestionCreatedWebConsumer(IHubContext<DiscussionHub> hubContext, ILogger<QuestionCreatedWebConsumer> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<QuestionCreated> context)
    {
        var msg = context.Message;
        _logger.LogInformation("[SignalR Consumer] QuestionCreated received for product {ProductId}, question {QuestionId}", msg.ProductId, msg.QuestionId);

        var eventPayload = new
        {
            Id = msg.QuestionId,
            msg.ProductId,
            msg.ProductName,
            msg.QuestionText,
            msg.UserName,
            FormattedUserName = FormatDisplayName(msg.UserName),
            msg.CreatedAt,
            Status = 0
        };

        // 1. Ürün sayfasını dinleyen kullanıcılara anlık bas
        await _hubContext.Clients.Group($"product-{msg.ProductId}")
            .SendAsync("ReceiveNewQuestion", eventPayload);

        // 2. Admin paneline bildirim ve canlı tablo güncellemesi bas
        await _hubContext.Clients.Group("admin-discussions")
            .SendAsync("ReceiveAdminNewQuestion", eventPayload);
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

public class QuestionAnsweredWebConsumer : IConsumer<QuestionAnswered>
{
    private readonly IHubContext<DiscussionHub> _hubContext;
    private readonly ILogger<QuestionAnsweredWebConsumer> _logger;

    public QuestionAnsweredWebConsumer(IHubContext<DiscussionHub> hubContext, ILogger<QuestionAnsweredWebConsumer> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<QuestionAnswered> context)
    {
        var msg = context.Message;
        _logger.LogInformation("[SignalR Consumer] QuestionAnswered received for question {QuestionId}, product {ProductId}", msg.QuestionId, msg.ProductId);

        var answerPayload = new
        {
            msg.QuestionId,
            msg.AnswerId,
            msg.ProductId,
            msg.ProductName,
            msg.AnswerText,
            msg.ResponderName,
            msg.IsEdit,
            msg.CreatedAt
        };

        // 1. Ürün sayfasında bekleyen kullanıcılara satıcı cevabını push et
        await _hubContext.Clients.Group($"product-{msg.ProductId}")
            .SendAsync("ReceiveQuestionAnswered", answerPayload);

        // 2. Soruyu soran kullanıcıya (sitenin neresinde olursa olsun) özel push et!
        if (!string.IsNullOrEmpty(msg.UserId))
        {
            await _hubContext.Clients.Group($"user-{msg.UserId}")
                .SendAsync("ReceiveMyQuestionAnswered", answerPayload);
        }

        // 3. Admin paneline canlı güncellenme push
        await _hubContext.Clients.Group("admin-discussions")
            .SendAsync("ReceiveAdminQuestionAnswered", answerPayload);
    }
}

public class QuestionDeletedWebConsumer : IConsumer<QuestionDeleted>
{
    private readonly IHubContext<DiscussionHub> _hubContext;
    private readonly ILogger<QuestionDeletedWebConsumer> _logger;

    public QuestionDeletedWebConsumer(IHubContext<DiscussionHub> hubContext, ILogger<QuestionDeletedWebConsumer> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<QuestionDeleted> context)
    {
        var msg = context.Message;
        _logger.LogInformation("[SignalR Consumer] QuestionDeleted received for question {QuestionId}", msg.QuestionId);

        var deletePayload = new
        {
            msg.QuestionId,
            msg.ProductId
        };

        if (!string.IsNullOrEmpty(msg.ProductId))
        {
            await _hubContext.Clients.Group($"product-{msg.ProductId}")
                .SendAsync("ReceiveQuestionDeleted", deletePayload);
        }

        await _hubContext.Clients.Group("admin-discussions")
            .SendAsync("ReceiveAdminQuestionDeleted", deletePayload);
    }
}
