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
            var userId = _identityService.GetUserId();
            request.Headers.Add("X-User-Id", userId);

            // Access token'ı cookie'den al ve Authorization header'ına ekle
            var accessToken = await _httpContextAccessor.HttpContext?.GetTokenAsync("access_token")!;
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
