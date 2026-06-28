using GameGaraj.Order.Application.Commands;
using GameGaraj.Order.Domain.Entities;
using GameGaraj.Order.Infrastructure;
using GameGaraj.Shared.Observability.Metrics;
using MediatR;

namespace GameGaraj.Order.Application.Handlers
{
    public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, int>
    {
        private readonly OrderDbContext _context;
        private readonly OrderMetrics _metrics;

        public CreateOrderCommandHandler(OrderDbContext context, OrderMetrics metrics)
        {
            _context = context;
            _metrics = metrics;
        }

        public async Task<int> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            var newOrder = new Domain.Entities.Order
            {
                BuyerId = request.BuyerId,
                CreatedDate = DateTime.Now,
                OriginalTotalAmount = request.OriginalTotalAmount,
                CampaignDiscountAmount = request.CampaignDiscountAmount,
                CouponDiscountAmount = request.CouponDiscountAmount,
                ShippingFee = request.ShippingFee,
                TotalPaidAmount = request.TotalPaidAmount,
                CouponCode = request.CouponCode,
                AppliedCampaignName = request.AppliedCampaignName,
                Status = 0, // Beklemede
                DeliveryAddress = new Address
                {
                    FirstName = request.Address.FirstName,
                    LastName = request.Address.LastName,
                    PhoneNumber = request.Address.PhoneNumber,
                    Email = request.Address.Email,
                    Province = request.Address.Province,
                    District = request.Address.District,
                    Neighborhood = request.Address.Neighborhood,
                    PostalCode = request.Address.PostalCode,
                    AddressDetail = request.Address.AddressDetail
                },
                InvoiceAddress = new Address
                {
                    FirstName = request.Address.FirstName,
                    LastName = request.Address.LastName,
                    PhoneNumber = request.Address.PhoneNumber,
                    Email = request.Address.Email,
                    Province = request.Address.Province,
                    District = request.Address.District,
                    Neighborhood = request.Address.Neighborhood,
                    PostalCode = request.Address.PostalCode,
                    AddressDetail = request.Address.AddressDetail
                }
            };

            // OrderItems mapping
            foreach (var item in request.OrderItems)
            {
                newOrder.OrderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    PictureUrl = item.PictureUrl,
                    Price = item.Price,
                    Quantity = item.Quantity > 0 ? item.Quantity : 1,
                    DiscountAmount = item.DiscountAmount
                });
            }

            // --- PROFESYONEL LEDGER (AUDIT TRAIL) OLUŞTURMA ---
            int sortOrder = 1;

            // 1. Ara Toplam
            newOrder.OrderPricingLedgers.Add(new OrderPricingLedger
            {
                Title = "Ürünlerin Toplamı",
                Amount = request.OriginalTotalAmount,
                Type = LedgerRowType.SubTotal,
                SortOrder = sortOrder++
            });

            // 2. Kampanya İndirimleri
            if (request.OrderDiscounts != null && request.OrderDiscounts.Any())
            {
                foreach (var discount in request.OrderDiscounts)
                {
                    newOrder.OrderPricingLedgers.Add(new OrderPricingLedger
                    {
                        Title = $"Kampanya: {discount.Title}",
                        Amount = discount.Amount,
                        Type = LedgerRowType.Discount,
                        SortOrder = sortOrder++
                    });
                }
            }
            else if (request.CampaignDiscountAmount > 0)
            {
                newOrder.OrderPricingLedgers.Add(new OrderPricingLedger
                {
                    Title = string.IsNullOrWhiteSpace(request.AppliedCampaignName) ? "Kampanya İndirimi" : $"Kampanya: {request.AppliedCampaignName}",
                    Amount = request.CampaignDiscountAmount,
                    Type = LedgerRowType.Discount,
                    SortOrder = sortOrder++
                });
            }

            // 3. Kupon İndirimi
            if (request.CouponDiscountAmount > 0)
            {
                newOrder.OrderPricingLedgers.Add(new OrderPricingLedger
                {
                    Title = string.IsNullOrWhiteSpace(request.CouponCode) ? "Kupon İndirimi" : $"Kupon: {request.CouponCode}",
                    Amount = request.CouponDiscountAmount,
                    Type = LedgerRowType.Discount,
                    SortOrder = sortOrder++
                });
            }

            // 4. Kargo
            newOrder.OrderPricingLedgers.Add(new OrderPricingLedger
            {
                Title = "Kargo Ücreti",
                Amount = request.ShippingFee,
                Type = LedgerRowType.Fee,
                SortOrder = sortOrder++
            });

            // 5. NET TOPLAM
            newOrder.OrderPricingLedgers.Add(new OrderPricingLedger
            {
                Title = "GENEL TOPLAM",
                Amount = request.TotalPaidAmount,
                Type = LedgerRowType.TransactionTotal,
                SortOrder = sortOrder++
            });

            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync(cancellationToken);

            _metrics.OrderCreated(request.BuyerId);

            return newOrder.Id;
        }
    }
}
