using System.Diagnostics;
using GameGaraj.Order.Domain.Enums;
using GameGaraj.Order.Infrastructure;
using GameGaraj.Shared.Events;
using GameGaraj.Shared.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Order.API.Services.Hosted
{
    public class OrderExpirationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OrderExpirationWorker> _logger;
        private readonly TimeSpan _timeoutPeriod = TimeSpan.FromMinutes(10); // 10 minutes timeout

        public OrderExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<OrderExpirationWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[OrderExpirationWorker] Background service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckExpiredOrdersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[OrderExpirationWorker] Error occurred while checking expired orders.");
                }

                // Check every 1 minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckExpiredOrdersAsync(CancellationToken cancellationToken)
        {
            using (var activity = AppDiagnostics.StartActivity("Cancel Expired Orders"))
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                // Compare with Local time since newOrder.CreatedDate is saved as DateTime.Now (local)
                var expirationThreshold = DateTime.Now.Subtract(_timeoutPeriod);

                var expiredOrders = await context.Orders
                    .Include(x => x.OrderItems)
                    .Where(x => x.Status == (int)OrderStatus.Pending && x.CreatedDate < expirationThreshold)
                    .ToListAsync(cancellationToken);

                if (expiredOrders.Any())
                {
                    activity?.SetTag("cancelled.orders.count", expiredOrders.Count);
                    _logger.LogWarning($"[OrderExpirationWorker] Found {expiredOrders.Count} expired orders to cancel.");

                    foreach (var order in expiredOrders)
                    {
                        _logger.LogWarning($"[OrderExpirationWorker] Cancelling Order #{order.Id} (Created at: {order.CreatedDate}, Status: Pending).");

                        order.Status = (int)OrderStatus.Failed;

                        // Publish PaymentFailed event to release reserved stocks in Catalog API
                        await publishEndpoint.Publish(new PaymentFailed
                        {
                            OrderId = order.Id,
                            Reason = "Ödeme süresi doldu (Zaman aşımı)",
                            OrderItems = order.OrderItems.Select(x => new OrderItemMessage
                            {
                                ProductId = x.ProductId,
                                Quantity = x.Quantity
                            }).ToList()
                        }, cancellationToken);
                    }

                    await context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("[OrderExpirationWorker] Successfully cancelled expired orders and published compensation events.");
                }
            }
        }
    }
}
