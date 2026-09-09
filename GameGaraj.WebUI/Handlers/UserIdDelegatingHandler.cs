using GameGaraj.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authentication;

namespace GameGaraj.WebUI.Handlers
{
    public class UserIdDelegatingHandler : DelegatingHandler
    {
        private readonly IIdentityService _identityService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserIdDelegatingHandler(IIdentityService identityService, IHttpContextAccessor httpContextAccessor)
        {
            _identityService = identityService;
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            if (!request.Headers.Contains("X-User-Id"))
            {
                var userId = _identityService.GetUserId();
                request.Headers.Add("X-User-Id", userId);
            }

            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                if (!request.Headers.Contains("X-User-Email"))
                {
                    var email = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value 
                                ?? user.FindFirst("email")?.Value;
                    if (!string.IsNullOrEmpty(email))
                    {
                        request.Headers.Add("X-User-Email", email);
                    }
                }

                if (!request.Headers.Contains("X-User-Name"))
                {
                    var givenName = user.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? user.FindFirst("given_name")?.Value;
                    var surname = user.FindFirst(System.Security.Claims.ClaimTypes.Surname)?.Value ?? user.FindFirst("family_name")?.Value;
                    var fullName = !string.IsNullOrEmpty(givenName) && !string.IsNullOrEmpty(surname) 
                                   ? $"{givenName} {surname}" 
                                   : null;

                    var name = fullName
                               ?? user.FindFirst("name")?.Value
                               ?? user.Identity?.Name 
                               ?? user.FindFirst("preferred_username")?.Value;

                    if (!string.IsNullOrEmpty(name))
                    {
                        // HTTP header değerleri ASCII olmalıdır; Türkçe karakterler için URL-encode yapılıyor
                        request.Headers.Add("X-User-Name", Uri.EscapeDataString(name));
                    }
                }

                if (!request.Headers.Contains("X-User-Role"))
                {
                    var roles = user.FindAll(System.Security.Claims.ClaimTypes.Role)
                                    .Concat(user.FindAll("role"))
                                    .Select(c => c.Value)
                                    .Distinct();
                    var roleString = string.Join(",", roles);
                    if (!string.IsNullOrEmpty(roleString))
                    {
                        request.Headers.Add("X-User-Role", roleString);
                    }
                }
            }

            // Access token'ı cookie'den al ve Authorization header'ına ekle
            if (!request.Headers.Contains("Authorization"))
            {
                var accessToken = await _httpContextAccessor.HttpContext?.GetTokenAsync("access_token")!;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    request.Headers.Add("Authorization", $"Bearer {accessToken}");
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
