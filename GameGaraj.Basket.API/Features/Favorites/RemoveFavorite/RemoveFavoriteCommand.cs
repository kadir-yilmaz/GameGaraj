using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Favorites.RemoveFavorite;

public record RemoveFavoriteCommand(string ProductId) : IRequest<ServiceResult>;
