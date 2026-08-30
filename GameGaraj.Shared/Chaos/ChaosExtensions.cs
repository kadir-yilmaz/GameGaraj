using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace GameGaraj.Shared.Chaos
{
    public static class ChaosExtensions
    {
        public static IServiceCollection AddChaosServices(this IServiceCollection services)
        {
            services.AddSingleton<IChaosManager, ChaosManager>();
            return services;
        }

        public static IApplicationBuilder UseChaos(this IApplicationBuilder app, string serviceName)
        {
            return app.UseMiddleware<ChaosMiddleware>(serviceName);
        }
    }
}
