using System.Diagnostics;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Shared.Events;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Shared.Observability;
using Microsoft.Extensions.Caching.Distributed;
using MassTransit;

namespace GameGaraj.Catalog.API.Consumers
{
    public class PaymentCompletedConsumer : IConsumer<PaymentCompleted>
    {
        private readonly CatalogDbContext _context;
        private readonly ILogger<PaymentCompletedConsumer> _logger;
        private readonly IDistributedCache? _cache;

        public PaymentCompletedConsumer(
            CatalogDbContext context,
            ILogger<PaymentCompletedConsumer> logger,
            IDistributedCache? cache = null)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        public async Task Consume(ConsumeContext<PaymentCompleted> context)
        {
            _logger.LogInformation($"[PaymentCompletedConsumer] Payment successful for OrderId: {context.Message.OrderId}. Finalizing stock.");

            using (var activity = AppDiagnostics.StartActivity("Complete Saga"))
            {
                activity?.SetTag("order.id", context.Message.OrderId);
                activity?.SetTag("saga.step", "StockFinalization");

                foreach (var item in context.Message.OrderItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        activity?.SetTag($"product.id.{product.Id}", product.Id);
                        _logger.LogInformation($"[PaymentCompletedConsumer] Processing stock for {product.Name}. Current Total: {product.Stock}, Current Reserved: {product.ReservedStock}");
 
                         // Ödeme başarılı: Fiziksel stoğu her durumda düş (failsafe)
                         product.Stock -= item.Quantity;
 
                         // Rezervasyonu sadece varsa ve miktar yeterliyse düş
                         if (product.ReservedStock >= item.Quantity)
                         {
                             product.ReservedStock -= item.Quantity;
                             _logger.LogInformation($"[PaymentCompletedConsumer] Deducted from ReservedStock for {product.Name}");
                         }
                         else if (product.ReservedStock > 0)
                         {
                             _logger.LogWarning($"[PaymentCompletedConsumer] Partial ReservedStock ({product.ReservedStock}) for {product.Name}. Clearing it.");
                             product.ReservedStock = 0;
                         }
 
                         await _context.SaveChangesAsync();
                         _logger.LogInformation($"[PaymentCompletedConsumer] Stock finalized for {product.Name}. New Total: {product.Stock}, New Reserved: {product.ReservedStock}");
                         
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
