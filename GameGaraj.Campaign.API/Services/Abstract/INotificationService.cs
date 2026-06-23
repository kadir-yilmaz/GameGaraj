using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Services.Abstract
{
    public interface INotificationService
    {
        Task<List<UserNotification>> GetByUserIdAsync(string userId, bool unreadOnly = false);
        Task<int> GetUnreadCountAsync(string userId);
        Task CreateAsync(UserNotification notification);
        Task MarkAsReadAsync(int id);
        Task MarkAllAsReadAsync(string userId);
    }
}
