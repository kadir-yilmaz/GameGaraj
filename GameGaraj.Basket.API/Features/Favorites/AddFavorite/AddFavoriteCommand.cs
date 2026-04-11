using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Favorites.AddFavorite;

public record AddFavoriteCommand(string ProductId) : IRequest<ServiceResult>;
