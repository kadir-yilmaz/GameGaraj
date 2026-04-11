using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Favorites.GetFavorites;

public record GetFavoritesQuery() : IRequest<ServiceResult<GetFavoritesResponse>>;

public record GetFavoritesResponse(string UserId, List<string> ProductIds);
