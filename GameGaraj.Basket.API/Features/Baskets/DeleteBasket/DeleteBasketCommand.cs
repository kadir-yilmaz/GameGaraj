using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.DeleteBasket;

public record DeleteBasketCommand : IRequest<ServiceResult>;
