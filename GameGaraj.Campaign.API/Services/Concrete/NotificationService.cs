using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;

namespace GameGaraj.Campaign.API.Services.Concrete
{
    public class NotificationService : INotificationService
    {
        private readonly string _connectionString;

        public NotificationService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SqlServer")!;
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<List<UserNotification>> GetByUserIdAsync(string userId, bool unreadOnly = false)
        {
            var query = @"SELECT Id, UserId, Title, Message, IconClass, LinkUrl, IsRead, CreatedTime
                          FROM UserNotifications 
                          WHERE UserId = @UserId";

            if (unreadOnly)
                query += " AND IsRead = 0";

            query += " ORDER BY CreatedTime DESC";

            using var connection = CreateConnection();
            var notifications = await connection.QueryAsync<UserNotification>(query, new { UserId = userId });
            return notifications.ToList();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            const string query = @"SELECT COUNT(1) FROM UserNotifications 
                                   WHERE UserId = @UserId AND IsRead = 0";

            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(query, new { UserId = userId });
        }

        public async Task CreateAsync(UserNotification notification)
        {
            const string query = @"INSERT INTO UserNotifications 
                                    (UserId, Title, Message, IconClass, LinkUrl, IsRead)
                                   VALUES 
                                    (@UserId, @Title, @Message, @IconClass, @LinkUrl, 0)";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(query, notification);
        }

        public async Task MarkAsReadAsync(int id)
        {
            const string query = "UPDATE UserNotifications SET IsRead = 1 WHERE Id = @Id";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(query, new { Id = id });
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            const string query = "UPDATE UserNotifications SET IsRead = 1 WHERE UserId = @UserId AND IsRead = 0";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(query, new { UserId = userId });
        }
    }
}
