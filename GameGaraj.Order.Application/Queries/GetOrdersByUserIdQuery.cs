using MediatR;
using GameGaraj.Order.Application.Dtos;

namespace GameGaraj.Order.Application.Queries
{
    /// <summary>
    /// Kullanıcıya ait siparişleri getirme sorgusu
    /// </summary>
    public class GetOrdersByUserIdQuery : IRequest<List<OrderDto>>
    {
        public string UserId { get; set; } = string.Empty;
    }
}
