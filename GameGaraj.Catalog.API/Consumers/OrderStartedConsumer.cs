using GameGaraj.Catalog.API.Data;
using GameGaraj.Shared.Events;
using GameGaraj.Catalog.API.Models;
using Microsoft.Extensions.Caching.Distributed;
using MassTransit;

namespace GameGaraj.Catalog.API.Consumers
{
    public class OrderStartedConsumer : IConsumer<OrderStarted>
    {
        private readonly CatalogDbContext _context;
        private readonly ILogger<OrderStartedConsumer> _logger;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IDistributedCache? _cache;

        public OrderStartedConsumer(
            CatalogDbContext context,
            ILogger<OrderStartedConsumer> logger,
            IPublishEndpoint publishEndpoint,
            IDistributedCache? cache = null)
        {
            _context = context;
            _logger = logger;
            _publishEndpoint = publishEndpoint;
            _cache = cache;
        }

        public async Task Consume(ConsumeContext<OrderStarted> context)
        {
            _logger.LogInformation($"[OrderStartedConsumer] Processing order: {context.Message.OrderId}");

            bool allReserved = true;
            string failReason = "";

            foreach (var item in context.Message.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                {
                    allReserved = false;
                    failReason = $"Ürün bulunamadı: {item.ProductId}";
                    break;
                }

                if (product.AvailableStock < item.Quantity)
                {
                    allReserved = false;
                    failReason = $"Yetersiz stok: {product.Name} (Mevcut: {product.AvailableStock}, İstenen: {item.Quantity})";
                    break;
                }

                // Stok rezerve et
                product.ReservedStock += item.Quantity;
                await _context.SaveChangesAsync();
                _logger.LogInformation($"[OrderStartedConsumer] Reserved {item.Quantity} for {product.Name}");
                
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
                }
            }

            if (allReserved)
            {
                _logger.LogInformation($"[OrderStartedConsumer] All items reserved for OrderId: {context.Message.OrderId}");
                await _publishEndpoint.Publish(new StockReserved { OrderId = context.Message.OrderId });
            }
            else
            {
                _logger.LogWarning($"[OrderStartedConsumer] Reservation failed for OrderId: {context.Message.OrderId}. Reason: {failReason}");

                // Geri alma (Compensating Transaction) - bu basitleştirilmiş bir örnek. 
                // Gerçek senaryoda daha önce rezerve edilenleri geri bırakmak gerekir.
                // Şimdilik basitleştirilmiş tutuyoruz.

                await _publishEndpoint.Publish(new StockNotReserved
                {
                    OrderId = context.Message.OrderId,
                    Reason = failReason
                });
            }
        }
    }
}
