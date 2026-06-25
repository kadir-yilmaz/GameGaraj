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

            // All services route through the gateway with service-specific path prefixes
            var gatewayUri = new Uri($"{serviceApiSettings!.GatewayBaseUri}/");

            // Catalog Service
            services.AddHttpClient<ICatalogService, CatalogService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/catalog/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Basket Service
            services.AddHttpClient<IBasketService, BasketService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/basket/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Order Service
            services.AddHttpClient<IOrderService, OrderService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/order/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Review Service
            services.AddHttpClient<IReviewService, ReviewService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/review/");
                client.Timeout = TimeSpan.FromSeconds(4);
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Favorites Service (lives in basket-api, routed through gateway)
            services.AddHttpClient<IFavoritesService, FavoritesService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/favorites/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Payment Service
            services.AddHttpClient<IPaymentService, PaymentService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/payment/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Identity Service (talks directly to Keycloak, not through gateway)
            services.AddHttpClient<IIdentityService, IdentityService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            // PhotoStock Service
            services.AddHttpClient<IPhotoStockService, PhotoStockService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/photostock/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Campaign Service
            services.AddHttpClient<ICampaignService, CampaignService>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri, "api/campaign/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();
        }
    }
}
