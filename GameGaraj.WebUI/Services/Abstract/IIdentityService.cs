using GameGaraj.WebUI.Models.Auth;

namespace GameGaraj.WebUI.Services.Abstract
{
    public interface IIdentityService
    {
        Task<(string? Error, string? UserId)> SignInAsync(SignInViewModel model);
        Task<string?> SignUpAsync(SignUpViewModel model);
        Task<TokenResponse?> GetAccessTokenByRefreshTokenAsync();
        Task RevokeRefreshToken();
        string GetUserId();
    }
}
