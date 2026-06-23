using GameGaraj.Basket.API.Data;
using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.UpdateBasket;

public class UpdateBasketCommandHandler(BasketService basketService, IIdentityService identityService)
    : IRequestHandler<UpdateBasketCommand, ServiceResult>
{
    public async Task<ServiceResult> Handle(UpdateBasketCommand request, CancellationToken cancellationToken)
    {
        var basket = new Data.Basket
        {
            UserId = identityService.UserId,
            Items = request.Items.Select(x => new BasketItem
            {
                Id = x.Id,
                Name = x.Name,
                CategoryId = x.CategoryId ?? string.Empty,
                Price = x.Price,
                PictureUrl = x.PictureUrl,
                Quantity = x.Quantity,
                ProductSlug = x.ProductSlug,
                Brand = x.Brand
            }).ToList()
        };

        await basketService.SaveBasketAsync(basket, cancellationToken);

        return ServiceResult.SuccessAsNoContent();
    }
}
