using GameGaraj.Basket.API.Dtos;
using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.GetBasket;

public class GetBasketQueryHandler(BasketService basketService, IIdentityService identityService)
    : IRequestHandler<GetBasketQuery, ServiceResult<BasketDto>>
{
    public async Task<ServiceResult<BasketDto>> Handle(GetBasketQuery request, CancellationToken cancellationToken)
    {
        var basket = await basketService.GetBasketAsync(cancellationToken);

        if (basket is null)
        {
            return ServiceResult<BasketDto>.SuccessAsOk(new BasketDto(identityService.UserId, [], 0));
        }

        var basketDto = new BasketDto(
            basket.UserId,
            basket.Items.Select(x => new BasketItemDto(x.Id, x.Name, x.Price, x.PictureUrl, x.Quantity, x.CategoryId, x.Brand, x.ProductSlug)).ToList(),
            basket.TotalPrice);

        return ServiceResult<BasketDto>.SuccessAsOk(basketDto);
    }
}
