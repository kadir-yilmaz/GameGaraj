using System.Security.Claims;

namespace GameGaraj.Basket.API.Services;

public class IdentityService(IHttpContextAccessor httpContextAccessor) : IIdentityService
{
    public string UserId
    {
        get
        {
            // 1. Custom header'dan al (WebUI'den gelen istekler için)
            var customUserId = httpContextAccessor.HttpContext?.Request.Headers["X-User-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(customUserId))
                return customUserId;

            // 2. JWT'den al (Keycloak entegrasyonundan sonra)
            var jwtUserId = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                            ?? httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(jwtUserId))
                return jwtUserId;

            // 3. Fallback
            return "anonymous-user";
        }
    }

    public string UserName => httpContextAccessor.HttpContext?.User?.Identity?.Name ?? string.Empty;
}
