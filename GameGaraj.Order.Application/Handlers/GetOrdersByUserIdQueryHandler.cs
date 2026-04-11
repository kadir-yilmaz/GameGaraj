using MediatR;
using Microsoft.EntityFrameworkCore;
using GameGaraj.Order.Application.Dtos;
using GameGaraj.Order.Application.Mapping;
using GameGaraj.Order.Application.Queries;
using GameGaraj.Order.Infrastructure;

namespace GameGaraj.Order.Application.Handlers
{
    public class GetOrdersByUserIdQueryHandler : IRequestHandler<GetOrdersByUserIdQuery, List<OrderDto>>
    {
        private readonly OrderDbContext _context;

        public GetOrdersByUserIdQueryHandler(OrderDbContext context)
        {
            _context = context;
        }

        public async Task<List<OrderDto>> Handle(GetOrdersByUserIdQuery request, CancellationToken cancellationToken)
        {
            var orders = await _context.Orders
                .Include(x => x.OrderItems)
                .Include(x => x.DeliveryAddress)
                .Include(x => x.InvoiceAddress)
                .Where(x => x.BuyerId == request.UserId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync(cancellationToken);

            if (!orders.Any())
            {
                return new List<OrderDto>();
            }

            return ObjectMapper.Mapper.Map<List<OrderDto>>(orders);
        }
    }
}
