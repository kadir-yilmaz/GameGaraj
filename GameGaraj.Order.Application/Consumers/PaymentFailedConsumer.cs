using System.Diagnostics;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using GameGaraj.Order.Domain.Enums;
using GameGaraj.Order.Infrastructure;
using GameGaraj.Shared.Events;
using GameGaraj.Shared.Observability;

namespace GameGaraj.Order.Application.Consumers
{
    /// <summary>
    /// PaymentFailed event'ini dinler ve sipariş durumunu Failed olarak günceller.
    /// </summary>
    public class PaymentFailedConsumer : IConsumer<PaymentFailed>
    {
        private readonly OrderDbContext _context;

        public PaymentFailedConsumer(OrderDbContext context)
        {
            _context = context;
        }

        public async Task Consume(ConsumeContext<PaymentFailed> context)
        {
            Console.WriteLine($"[PaymentFailedConsumer] Received PaymentFailed for OrderId: {context.Message.OrderId}, Reason: {context.Message.Reason}");

            using (var activity = AppDiagnostics.StartActivity("Cancel Order"))
            {
                activity?.SetTag("order.id", context.Message.OrderId);
                activity?.SetTag("saga.step", "CancelOrder");
                activity?.SetTag("saga.status", "Failed");
                activity?.SetTag("saga.reason", context.Message.Reason);

                var order = await _context.Orders.FirstOrDefaultAsync(x => x.Id == context.Message.OrderId);
                
                if (order == null)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, $"Order not found: {context.Message.OrderId}");
                    Console.WriteLine($"[PaymentFailedConsumer] ❌ Order not found: {context.Message.OrderId}");
                    return;
                }

                order.Status = (int)OrderStatus.Failed;
                await _context.SaveChangesAsync();

                Console.WriteLine($"[PaymentFailedConsumer] ✅ Order {order.Id} status updated to Failed");
            }
        }
    }
}
