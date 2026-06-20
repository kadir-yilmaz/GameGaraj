using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.UpdateBasket;

public record UpdateBasketCommand(string? UserId, List<UpdateBasketItemDto> Items) : IRequest<ServiceResult>;

public record UpdateBasketItemDto(
    string Id, 
    string Name, 
    string CategoryId, 
    decimal Price, 
    string? PictureUrl, 
    int Quantity, 
    string? ProductSlug);
