using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Favorites.RemoveFavorite;

public class RemoveFavoriteCommandHandler(FavoritesService favoritesService)
    : IRequestHandler<RemoveFavoriteCommand, ServiceResult>
{
    public async Task<ServiceResult> Handle(RemoveFavoriteCommand request, CancellationToken cancellationToken)
    {
        await favoritesService.RemoveFavoriteAsync(request.ProductId, cancellationToken);
        return ServiceResult.SuccessAsNoContent();
    }
}
