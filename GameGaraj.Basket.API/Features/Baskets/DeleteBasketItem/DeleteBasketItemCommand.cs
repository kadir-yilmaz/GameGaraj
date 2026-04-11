using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.DeleteBasketItem;

public record DeleteBasketItemCommand(string ProductId) : IRequest<ServiceResult>;
