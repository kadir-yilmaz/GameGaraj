using System.Diagnostics;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Shared.Events;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Shared.Observability;
using Microsoft.Extensions.Caching.Distributed;
using MassTransit;

namespace GameGaraj.Catalog.API.Consumers
{
    public class PaymentFailedConsumer : IConsumer<PaymentFailed>
    {
        private readonly CatalogDbContext _context;
        private readonly ILogger<PaymentFailedConsumer> _logger;
        private readonly IDistributedCache? _cache;

        public PaymentFailedConsumer(
            CatalogDbContext context,
            ILogger<PaymentFailedConsumer> logger,
            IDistributedCache? cache = null)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        public async Task Consume(ConsumeContext<PaymentFailed> context)
        {
            _logger.LogWarning($"[PaymentFailedConsumer] Payment failed for OrderId: {context.Message.OrderId}. Releasing reserved stock.");

            using (var activity = AppDiagnostics.StartActivity("Stock Compensation"))
            {
                activity?.SetTag("order.id", context.Message.OrderId);
                activity?.SetTag("saga.step", "StockRelease");
                activity?.SetTag("saga.status", "Compensated");

                foreach (var item in context.Message.OrderItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        activity?.SetTag($"product.id.{product.Id}", product.Id);

                        // Ödeme başarısız: Rezervasyonu iade et (tekrar satılabilir yap)
                        if (product.ReservedStock >= item.Quantity)
                        {
                            product.ReservedStock -= item.Quantity;
                        }
                        else
                        {
                            product.ReservedStock = 0;
                        }
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"[PaymentFailedConsumer] Stock released for {product.Name}. Available: {product.AvailableStock}");
                        
                        // Elastisearch'e güncel veriyi gönder
                        _context.IndexingJobs.Add(new IndexingJob
                        {
                            Id = Guid.NewGuid().ToString(),
                            EntityType = "Product",
                            EntityId = product.Id,
                            Operation = IndexingJobOperation.Upsert,
                            Status = IndexingJobStatus.Pending,
                            CreatedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();

                        // Redis cache'i temizle
                        if (_cache != null)
                        {
                            await _cache.RemoveAsync($"product_{product.Id}");
                            await _cache.RemoveAsync($"product_slug_{product.Slug}");
                            await _cache.RemoveAsync("featured_products");
                        }
                    }
                }
            }

            await Task.CompletedTask;
        }
    }
}
