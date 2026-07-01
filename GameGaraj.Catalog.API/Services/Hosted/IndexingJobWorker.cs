using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Services.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace GameGaraj.Catalog.API.Services.Hosted
{
    public class IndexingJobWorker : BackgroundService
    {
        private const int MaxRetryCount = 5;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IndexingJobWorker> _logger;

        public IndexingJobWorker(IServiceScopeFactory scopeFactory, ILogger<IndexingJobWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Indexing job worker loop failed");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var indexService = scope.ServiceProvider.GetRequiredService<IProductIndexService>();
            var cache = scope.ServiceProvider.GetService<IDistributedCache>();

            var jobs = await context.IndexingJobs
                .Where(job =>
                    job.Status == IndexingJobStatus.Pending ||
                    (job.Status == IndexingJobStatus.Failed && job.RetryCount < MaxRetryCount))
                .OrderBy(job => job.CreatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var job in jobs)
            {
                await ProcessJobAsync(context, indexService, cache, job, cancellationToken);
            }
        }

        private async Task ProcessJobAsync(
            CatalogDbContext context,
            IProductIndexService indexService,
            IDistributedCache? cache,
            IndexingJob job,
            CancellationToken cancellationToken)
        {
            job.Status = IndexingJobStatus.Processing;
            job.LastAttemptAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            try
            {
                if (job.Operation == IndexingJobOperation.Delete)
                {
                    await indexService.DeleteAsync(job.EntityId);

                    // Elasticsearch'ten silindikten sonra Redis cache'i temizle
                    if (cache != null)
                    {
                        await cache.RemoveAsync($"product_{job.EntityId}", cancellationToken);
                        await cache.RemoveAsync("featured_products", cancellationToken);
                    }
                }
                else
                {
                    var product = await context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(item => item.Id == job.EntityId, cancellationToken);

                    if (product == null)
                    {
                        await indexService.DeleteAsync(job.EntityId);

                        if (cache != null)
                        {
                            await cache.RemoveAsync($"product_{job.EntityId}", cancellationToken);
                            await cache.RemoveAsync("featured_products", cancellationToken);
                        }
                    }
                    else
                    {
                        await indexService.IndexAsync(product);

                        // ⚠️ KRİTİK YARIŞ KOŞULU (RACE CONDITION) ÇÖZÜMÜ:
                        // Elasticsearch indekslemesi tamamen başarıyla tamamlandıktan sonra Redis önbelleğini (cache) temizliyoruz.
                        // Böylelikle, sonraki isteklerde cache miss olduğunda Elasticsearch'ten en güncel (stoku düşmüş) veri okunup tekrar cache'lenecektir.
                        if (cache != null)
                        {
                            await cache.RemoveAsync($"product_{product.Id}", cancellationToken);
                            await cache.RemoveAsync($"product_slug_{product.Slug}", cancellationToken);
                            await cache.RemoveAsync("featured_products", cancellationToken);
                            _logger.LogInformation($"[IndexingJobWorker] Invalidated Redis cache for product: {product.Name} and featured_products");
                        }
                    }
                }

                job.Status = IndexingJobStatus.Completed;
                job.ProcessedAt = DateTime.UtcNow;
                job.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.Status = IndexingJobStatus.Failed;
                job.ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                _logger.LogWarning(ex, "Indexing job {JobId} failed for {EntityType}:{EntityId}", job.Id, job.EntityType, job.EntityId);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
