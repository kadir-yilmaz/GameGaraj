using MassTransit;
using Microsoft.EntityFrameworkCore;
using GameGaraj.Order.Domain.Enums;
using GameGaraj.Order.Infrastructure;
using GameGaraj.Shared.Events;

namespace GameGaraj.Order.Application.Consumers
{
    /// <summary>
    /// PaymentCompleted event'ini dinler ve sipariş durumunu Completed olarak günceller.
    /// </summary>
    public class PaymentCompletedConsumer : IConsumer<PaymentCompleted>
    {
        private readonly OrderDbContext _context;

        public PaymentCompletedConsumer(OrderDbContext context)
        {
            _context = context;
        }

        public async Task Consume(ConsumeContext<PaymentCompleted> context)
        {
            Console.WriteLine($"[PaymentCompletedConsumer] Received PaymentCompleted for OrderId: {context.Message.OrderId}");

            var order = await _context.Orders.FirstOrDefaultAsync(x => x.Id == context.Message.OrderId);
            
            if (order == null)
            {
                Console.WriteLine($"[PaymentCompletedConsumer] ❌ Order not found: {context.Message.OrderId}");
                return;
            }

            // Ödeme başarılı, sipariş hazırlanmaya başlasın
            order.Status = (int)OrderStatus.Completed; // Önce Completed yap (ödeme onayı)
            await _context.SaveChangesAsync();

            Console.WriteLine($"[PaymentCompletedConsumer] ✅ Order {order.Id} status updated to Completed (Payment confirmed)");
        }
    }
}
