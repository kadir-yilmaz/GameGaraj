using System.Text.Json;
using GameGaraj.Basket.API.Data;
using Microsoft.Extensions.Caching.Distributed;

namespace GameGaraj.Basket.API.Services;

public class FavoritesService(IDistributedCache distributedCache, IIdentityService identityService)
{
    private string CacheKey => $"favorites:{identityService.UserId}";

    public async Task<Favorites> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        var favoritesString = await distributedCache.GetStringAsync(CacheKey, cancellationToken);
        
        if (string.IsNullOrEmpty(favoritesString))
        {
            return new Favorites { UserId = identityService.UserId };
        }

        try
        {
            return JsonSerializer.Deserialize<Favorites>(favoritesString) 
                   ?? new Favorites { UserId = identityService.UserId };
        }
        catch (JsonException)
        {
            await distributedCache.RemoveAsync(CacheKey, cancellationToken);
            return new Favorites { UserId = identityService.UserId };
        }
    }

    public async Task SaveFavoritesAsync(Favorites favorites, CancellationToken cancellationToken = default)
    {
        favorites.UserId = identityService.UserId;
        var favoritesString = JsonSerializer.Serialize(favorites);
        await distributedCache.SetStringAsync(CacheKey, favoritesString, cancellationToken);
    }

    public async Task<bool> IsFavoriteAsync(string productId, CancellationToken cancellationToken = default)
    {
        var favorites = await GetFavoritesAsync(cancellationToken);
        return favorites.ProductIds.Contains(productId);
    }

    public async Task AddFavoriteAsync(string productId, CancellationToken cancellationToken = default)
    {
        var favorites = await GetFavoritesAsync(cancellationToken);
        if (!favorites.ProductIds.Contains(productId))
        {
            favorites.ProductIds.Add(productId);
            await SaveFavoritesAsync(favorites, cancellationToken);
        }
    }

    public async Task RemoveFavoriteAsync(string productId, CancellationToken cancellationToken = default)
    {
        var favorites = await GetFavoritesAsync(cancellationToken);
        if (favorites.ProductIds.Remove(productId))
        {
            await SaveFavoritesAsync(favorites, cancellationToken);
        }
    }
}
