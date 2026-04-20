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

            // Catalog Service - Direkt Catalog API'ye bağlan (Gateway olmadan)
            services.AddHttpClient<ICatalogService, CatalogService>(client =>
            {
                client.BaseAddress = new Uri($"{serviceApiSettings!.CatalogUri}/");
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Basket Service - Direkt Basket API'ye bağlan (Gateway olmadan)
            services.AddHttpClient<IBasketService, BasketService>(client =>
            {
                client.BaseAddress = new Uri($"{serviceApiSettings!.BasketUri}/");
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Order Service - Direkt Order API'ye bağlan (Gateway olmadan)
            services.AddHttpClient<IOrderService, OrderService>(client =>
            {
                // OrderUri zaten /api ile bitiyor, tekrar eklemeyelim
                client.BaseAddress = new Uri($"{serviceApiSettings!.OrderUri}/");
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Favorites Service - BasketUri kullanır (favorites Basket.API içinde)
            services.AddHttpClient<IFavoritesService, FavoritesService>(client =>
            {
                client.BaseAddress = new Uri($"{serviceApiSettings!.BasketUri}/");
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Payment Service - Direkt Payment API'ye bağlan (Gateway olmadan)
            services.AddHttpClient<IPaymentService, PaymentService>(client =>
            {
                client.BaseAddress = new Uri($"{serviceApiSettings!.PaymentUri}/");
            });
            
            // Identity Service
            services.AddHttpClient<IIdentityService, IdentityService>();
            
            // PhotoStock Service - Gateway üzerinden değil, direkt PhotoStock API'ye
            services.AddHttpClient<IPhotoStockService, PhotoStockService>(client =>
            {
                client.BaseAddress = new Uri($"{serviceApiSettings!.PhotoStockUri}/");
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();

            // Campaign Service - Direkt Campaign API'ye bağlan
            services.AddHttpClient<ICampaignService, CampaignService>(client =>
            {
                client.BaseAddress = new Uri($"{serviceApiSettings!.CampaignUri}/");
            });

            // Discount Service - Direkt Discount API'ye bağlan
            services.AddHttpClient<IDiscountService, DiscountService>(client =>
            {
                client.BaseAddress = new Uri($"{serviceApiSettings!.DiscountUri}/");
            })
            .AddHttpMessageHandler<UserIdDelegatingHandler>();
        }
    }
}
