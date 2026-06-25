using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace GameGaraj.Review.API.Services;

public class OrderOwnershipClient : IOrderOwnershipClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OrderOwnershipClient> _logger;

    public OrderOwnershipClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IMemoryCache cache, ILogger<OrderOwnershipClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _logger = logger;
    }

    public async Task<OrderOwnershipResult> GetOwnershipAsync(string userId, string productId, CancellationToken cancellationToken)
    {
        var cacheKey = $"review-ownership:{userId}:{productId}".ToLowerInvariant();
        if (_cache.TryGetValue(cacheKey, out OrderOwnershipResult? cached) && cached != null)
        {
            _logger.LogInformation("Order ownership cache hit for user {UserId}, product {ProductId}, owns {Owns}", userId, productId, cached.Owns);
            return cached;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"orders/{Uri.EscapeDataString(userId)}/owns/{Uri.EscapeDataString(productId)}");
            var token = await _httpContextAccessor.HttpContext?.GetTokenAsync("access_token")!;
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Order ownership check failed with {StatusCode} for user {UserId}, product {ProductId}", response.StatusCode, userId, productId);
                return new OrderOwnershipResult();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OrderOwnershipResult>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new OrderOwnershipResult();

            var cacheDuration = result.Owns ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(1);
            _cache.Set(cacheKey, result, cacheDuration);
            _logger.LogInformation("Order ownership check completed in {ElapsedMs} ms for user {UserId}, product {ProductId}, owns {Owns}", stopwatch.ElapsedMilliseconds, userId, productId, result.Owns);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order ownership check failed for user {UserId}, product {ProductId}", userId, productId);
            return new OrderOwnershipResult();
        }
    }
}
