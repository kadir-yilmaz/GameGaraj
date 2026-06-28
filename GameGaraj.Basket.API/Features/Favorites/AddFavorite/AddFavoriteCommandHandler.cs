using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using GameGaraj.Shared.Observability.Metrics;
using MediatR;

namespace GameGaraj.Basket.API.Features.Favorites.AddFavorite;

public class AddFavoriteCommandHandler(FavoritesService favoritesService, BasketMetrics basketMetrics)
    : IRequestHandler<AddFavoriteCommand, ServiceResult>
{
    public async Task<ServiceResult> Handle(AddFavoriteCommand request, CancellationToken cancellationToken)
    {
        await favoritesService.AddFavoriteAsync(request.ProductId, cancellationToken);
        
        basketMetrics.FavoriteAdded();

        return ServiceResult.SuccessAsNoContent();
    }
}
