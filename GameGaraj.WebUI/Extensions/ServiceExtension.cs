using GameGaraj.WebUI.Handlers;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Services.Concrete;
using GameGaraj.WebUI.Settings;

namespace GameGaraj.WebUI.Extensions
{
    public static class ServiceExtension
    {
        public static void AddHttpClientServices(this IServiceCollection services, IConfiguration configuration)
        {
            var serviceApiSettings = configuration.GetSection("ServiceApiSettings").Get<ServiceApiSettings>();

            // Register DelegatingHandler
            services.AddTransient<UserIdDelegatingHandler>();
            services.AddTransient<OutboundRequestLoggingHandler>();

            // All services route through the gateway with service-specific path prefixes
            var gatewayUri = new Uri($"{serviceApiSettings!.GatewayBaseUri}/");

            // Catalog Service
            services.AddHttpClient<ICatalogService, CatalogService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/catalog/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<OutboundRequestLoggingHandler>()
            .AddHttpMessageHandler<UserIdDelegatingHandler>()
            .AddConfiguredResilienceHandler("Catalog.API");

            // Basket Service
            services.AddHttpClient<IBasketService, BasketService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/basket/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<OutboundRequestLoggingHandler>()
            .AddHttpMessageHandler<UserIdDelegatingHandler>()
            .AddConfiguredResilienceHandler("Basket.API");

            // Order Service
            services.AddHttpClient<IOrderService, OrderService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/order/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<OutboundRequestLoggingHandler>()
            .AddHttpMessageHandler<UserIdDelegatingHandler>()
            .AddConfiguredResilienceHandler("Order.API");

            // Review Service
            services.AddHttpClient<IReviewService, ReviewService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/review/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<OutboundRequestLoggingHandler>()
            .AddHttpMessageHandler<UserIdDelegatingHandler>()
            .AddConfiguredResilienceHandler("Review.API");

            // Favorites Service (lives in basket-api, routed through gateway)
            services.AddHttpClient<IFavoritesService, FavoritesService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/favorites/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<OutboundRequestLoggingHandler>()
            .AddHttpMessageHandler<UserIdDelegatingHandler>()
            .AddConfiguredResilienceHandler("Favorites.API");

            // Payment Service (No retry to prevent double charge; strict timeout only)
            services.AddHttpClient<IPaymentService, PaymentService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/payment/");
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddHttpMessageHandler<OutboundRequestLoggingHandler>()
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Identity Service (talks directly to Keycloak, not through gateway)
            services.AddHttpClient<IIdentityService, IdentityService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<OutboundRequestLoggingHandler>()
            .AddConfiguredResilienceHandler("Identity Service (Keycloak)");

            // PhotoStock Service
            services.AddHttpClient<IPhotoStockService, PhotoStockService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/photostock/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<OutboundRequestLoggingHandler>()
            .AddHttpMessageHandler<UserIdDelegatingHandler>()
            .AddConfiguredResilienceHandler("PhotoStock.API");

            // Campaign Service
            services.AddHttpClient<ICampaignService, CampaignService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/campaign/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<OutboundRequestLoggingHandler>()
            .AddHttpMessageHandler<UserIdDelegatingHandler>()
            .AddConfiguredResilienceHandler("Campaign.API");

            // Search Service (Go + Gin Search API routed through Gateway)
            services.AddHttpClient<ISearchService, SearchService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/search/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<OutboundRequestLoggingHandler>()
            .AddHttpMessageHandler<UserIdDelegatingHandler>()
            .AddConfiguredResilienceHandler("Search.API");
        }

        private static IHttpClientBuilder AddConfiguredResilienceHandler(this IHttpClientBuilder builder, string serviceName)
        {
            builder.AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = false;
                options.Retry.Delay = TimeSpan.FromSeconds(2); // 2s -> 4s -> 8s
                options.Retry.OnRetry = args =>
                {
                    var attempt = args.AttemptNumber + 1;
                    var delaySec = args.RetryDelay.TotalSeconds;
                    var statusCode = args.Outcome.Result?.StatusCode;
                    var statusStr = statusCode != null ? $"HTTP {(int)statusCode}" : (args.Outcome.Exception?.Message ?? "Bağlantı Hatası");

                    _ = PipelineNotifier.Instance?.NotifyAsync(
                        "Polly (Resilience)",
                        $"⚠️ [Polly Retry {attempt}/3] <b>{serviceName}</b> uykuda/kapalı ({statusStr}). <span class='badge bg-warning text-dark'>{delaySec:F0} sn</span> sonra tekrar denenecek...",
                        "warning"
                    );
                    return default;
                };
            });
            return builder;
        }
    }
}
