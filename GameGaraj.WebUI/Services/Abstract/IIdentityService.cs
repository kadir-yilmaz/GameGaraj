using GameGaraj.WebUI.Models.Auth;

namespace GameGaraj.WebUI.Services.Abstract
{
    public class UserSearchViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string DisplayName => $"{FirstName} {LastName} ({Email})";
    }

    public interface IIdentityService
    {
        Task<(string? Error, string? UserId)> SignInAsync(SignInViewModel model);
        Task<string?> SignUpAsync(SignUpViewModel model);
        Task<TokenResponse?> GetAccessTokenByRefreshTokenAsync(string? refreshToken = null);
        Task RevokeRefreshToken();
        string GetUserId();
        Task<List<UserSearchViewModel>> SearchUsersAsync(string query);
    }
}
