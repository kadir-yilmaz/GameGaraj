using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Favorites.AddFavorite;

public class AddFavoriteCommandHandler(FavoritesService favoritesService)
    : IRequestHandler<AddFavoriteCommand, ServiceResult>
{
    public async Task<ServiceResult> Handle(AddFavoriteCommand request, CancellationToken cancellationToken)
    {
        await favoritesService.AddFavoriteAsync(request.ProductId, cancellationToken);
        return ServiceResult.SuccessAsNoContent();
    }
}
