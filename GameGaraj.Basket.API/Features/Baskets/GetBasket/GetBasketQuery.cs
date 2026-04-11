using GameGaraj.Basket.API.Dtos;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.GetBasket;

public record GetBasketQuery : IRequest<ServiceResult<BasketDto>>;
