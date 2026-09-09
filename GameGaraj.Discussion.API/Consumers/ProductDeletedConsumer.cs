using GameGaraj.Discussion.API.Data;
using GameGaraj.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Discussion.API.Consumers;

/// <summary>
/// Ürün silindiğinde ilgili soruları arşivler (Madde 13).
/// Arşivlenen sorular public tarafta gösterilmez ama admin panelde "ürün artık mevcut değil" notuyla kalır.
/// </summary>
public class ProductDeletedConsumer : IConsumer<ProductDeleted>
{
    private readonly DiscussionDbContext _context;
    private readonly ILogger<ProductDeletedConsumer> _logger;

    public ProductDeletedConsumer(DiscussionDbContext context, ILogger<ProductDeletedConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProductDeleted> context)
    {
        var productId = context.Message.ProductId;

        var questions = await _context.Questions
            .IgnoreQueryFilters()
            .Where(q => q.ProductId == productId && !q.IsArchived && !q.IsDeleted)
            .ToListAsync(context.CancellationToken);

        if (questions.Count == 0)
        {
            _logger.LogInformation("No questions to archive for deleted product {ProductId}", productId);
            return;
        }

        foreach (var question in questions)
        {
            question.IsArchived = true;
            question.AdminNote = $"Ürün silindi ({context.Message.DeletedAt:dd.MM.yyyy HH:mm})";
        }

        await _context.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Archived {Count} questions for deleted product {ProductId}",
            questions.Count, productId);
    }
}
