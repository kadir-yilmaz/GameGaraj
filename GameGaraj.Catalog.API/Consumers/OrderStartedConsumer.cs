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
            var reservedProducts = new List<(Product Product, int ReservedQuantity)>();

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
                
                // Takip listesine ekle
                reservedProducts.Add((product, item.Quantity));
                
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

            if (allReserved)
            {
                _logger.LogInformation($"[OrderStartedConsumer] All items reserved for OrderId: {context.Message.OrderId}");
                await _publishEndpoint.Publish(new StockReserved { OrderId = context.Message.OrderId });
            }
            else
            {
                _logger.LogWarning($"[OrderStartedConsumer] Reservation failed for OrderId: {context.Message.OrderId}. Reason: {failReason}");

                // Geri alma (Compensating Transaction) - Rezerve edilen ürünlerin kilitlerini serbest bırak
                foreach (var (reservedProduct, reservedQuantity) in reservedProducts)
                {
                    try
                    {
                        if (reservedProduct.ReservedStock >= reservedQuantity)
                        {
                            reservedProduct.ReservedStock -= reservedQuantity;
                        }
                        else
                        {
                            reservedProduct.ReservedStock = 0;
                        }
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"[OrderStartedConsumer] Rollback: Released {reservedQuantity} reserved units for {reservedProduct.Name}");

                        // Elasticsearch güncellemesi
                        _context.IndexingJobs.Add(new IndexingJob
                        {
                            Id = Guid.NewGuid().ToString(),
                            EntityType = "Product",
                            EntityId = reservedProduct.Id,
                            Operation = IndexingJobOperation.Upsert,
                            Status = IndexingJobStatus.Pending,
                            CreatedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();

                        // Redis cache temizleme
                        if (_cache != null)
                        {
                            await _cache.RemoveAsync($"product_{reservedProduct.Id}");
                            await _cache.RemoveAsync($"product_slug_{reservedProduct.Slug}");
                            await _cache.RemoveAsync("featured_products");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[OrderStartedConsumer] Failed to rollback reservation for {reservedProduct.Name}");
                    }
                }

                await _publishEndpoint.Publish(new StockNotReserved
                {
                    OrderId = context.Message.OrderId,
                    Reason = failReason
                });
            }
        }
    }
}
