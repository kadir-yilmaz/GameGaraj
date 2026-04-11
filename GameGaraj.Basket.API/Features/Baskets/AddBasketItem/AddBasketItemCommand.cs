using System.ComponentModel.DataAnnotations;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.AddBasketItem;

public record AddBasketItemCommand(
    [Required] string Id,
    [Required] string Name,
    [Required] decimal Price,
    string? PictureUrl,
    [Range(1, int.MaxValue)] int Quantity) : IRequest<ServiceResult>;
