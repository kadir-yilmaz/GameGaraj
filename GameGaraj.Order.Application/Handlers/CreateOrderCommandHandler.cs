using MediatR;
using GameGaraj.Order.Application.Commands;
using GameGaraj.Order.Application.Dtos;
using GameGaraj.Order.Domain.Entities;
using GameGaraj.Order.Infrastructure;

namespace GameGaraj.Order.Application.Handlers
{
    public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreatedOrderDto>
    {
        private readonly OrderDbContext _context;
        private readonly MassTransit.IPublishEndpoint _publishEndpoint;

        public CreateOrderCommandHandler(OrderDbContext context, MassTransit.IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
        }

        public async Task<CreatedOrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            // Teslimat adresi (snapshot)
            var deliveryAddress = new Address
            {
                Type = Domain.Enums.AddressType.Delivery,
                FirstName = request.Address.FirstName ?? "",
                LastName = request.Address.LastName ?? "",
                PhoneNumber = request.Address.PhoneNumber ?? "",
                Province = request.Address.Province,
                District = request.Address.District,
                Neighborhood = request.Address.Neighborhood ?? "",
                PostalCode = request.Address.PostalCode ?? "",
                AddressDetail = request.Address.AddressDetail ?? ""
            };

            var newOrder = new Domain.Entities.Order
            {
                BuyerId = request.BuyerId,
                CreatedDate = DateTime.Now,
                DeliveryAddress = deliveryAddress,
                InvoiceAddress = null // Şimdilik null, ileride eklenecek
            };

            foreach (var item in request.OrderItems)
            {
                newOrder.OrderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    PictureUrl = item.PictureUrl,
                    Price = item.Price
                });
            }

            await _context.Orders.AddAsync(newOrder, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Publish OrderStarted event
            var orderStartedEvent = new GameGaraj.Shared.Events.OrderStarted
            {
                OrderId = newOrder.Id,
                BuyerId = newOrder.BuyerId,
                OrderItems = newOrder.OrderItems.Select(x => new GameGaraj.Shared.Events.OrderItemMessage
                {
                    ProductId = x.ProductId,
                    Quantity = 1 // NOT: Mevcut yapıda OrderItem içinde Quantity yok, varsayılan 1 alıyoruz
                }).ToList()
            };

            await _publishEndpoint.Publish(orderStartedEvent, cancellationToken);

            return new CreatedOrderDto { OrderId = newOrder.Id };
        }
    }
}
