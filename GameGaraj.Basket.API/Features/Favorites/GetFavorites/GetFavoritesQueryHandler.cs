using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Favorites.GetFavorites;

public class GetFavoritesQueryHandler(FavoritesService favoritesService)
    : IRequestHandler<GetFavoritesQuery, ServiceResult<GetFavoritesResponse>>
{
    public async Task<ServiceResult<GetFavoritesResponse>> Handle(GetFavoritesQuery request, CancellationToken cancellationToken)
    {
        var favorites = await favoritesService.GetFavoritesAsync(cancellationToken);
        
        var response = new GetFavoritesResponse(favorites.UserId, favorites.ProductIds);
        
        return ServiceResult<GetFavoritesResponse>.SuccessAsOk(response);
    }
}
