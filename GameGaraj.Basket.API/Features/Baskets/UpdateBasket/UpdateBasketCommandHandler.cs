using GameGaraj.Basket.API.Data;
using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using GameGaraj.Shared.Observability.Metrics;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.UpdateBasket;

public class UpdateBasketCommandHandler(BasketService basketService, IIdentityService identityService, BasketMetrics basketMetrics)
    : IRequestHandler<UpdateBasketCommand, ServiceResult>
{
    public async Task<ServiceResult> Handle(UpdateBasketCommand request, CancellationToken cancellationToken)
    {
        var existingBasket = await basketService.GetBasketAsync(cancellationToken);
        var existingIds = existingBasket?.Items
            .Select(item => item.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

        var updatedIds = basket.Items
            .Select(item => item.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var _ in updatedIds.Except(existingIds, StringComparer.OrdinalIgnoreCase))
        {
            basketMetrics.ItemAdded();
        }

        foreach (var _ in existingIds.Except(updatedIds, StringComparer.OrdinalIgnoreCase))
        {
            basketMetrics.ItemRemoved();
        }

        return ServiceResult.SuccessAsNoContent();
    }
}
