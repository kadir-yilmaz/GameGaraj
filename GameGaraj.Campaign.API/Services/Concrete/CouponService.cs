using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;

namespace GameGaraj.Campaign.API.Services.Concrete
{
    public class CouponService : ICouponService
    {
        private readonly string _connectionString;

        public CouponService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SqlServer")!;
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<List<Coupon>> GetAllAsync()
        {
            const string query = @"SELECT Id, Code, UserId, CouponType, Amount, Rate, MaxDiscountAmount,
                                    MinOrderAmount, IsUsed, IsActive, IsEarnedReward, RewardRuleId,
                                    ExpirationDate, AllowWithOtherCampaigns, CreatedTime
                                   FROM Coupons ORDER BY CreatedTime DESC";

            using var connection = CreateConnection();
            var coupons = await connection.QueryAsync<Coupon>(query);
            return coupons.ToList();
        }

        public async Task<Coupon?> GetByIdAsync(int id)
        {
            const string query = @"SELECT Id, Code, UserId, CouponType, Amount, Rate, MaxDiscountAmount,
                                    MinOrderAmount, IsUsed, IsActive, IsEarnedReward, RewardRuleId,
                                    ExpirationDate, AllowWithOtherCampaigns, CreatedTime
                                   FROM Coupons WHERE Id = @Id";

            using var connection = CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<Coupon>(query, new { Id = id });
        }

        public async Task<Coupon?> GetByCodeAsync(string code)
        {
            const string query = @"SELECT Id, Code, UserId, CouponType, Amount, Rate, MaxDiscountAmount,
                                    MinOrderAmount, IsUsed, IsActive, IsEarnedReward, RewardRuleId,
                                    ExpirationDate, AllowWithOtherCampaigns, CreatedTime
                                   FROM Coupons WHERE Code = @Code AND IsActive = 1";

            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<Coupon>(query, new { Code = code });
        }

        public async Task<List<Coupon>> GetPublicCouponsAsync()
        {
            const string query = @"SELECT Id, Code, UserId, CouponType, Amount, Rate, MaxDiscountAmount,
                                    MinOrderAmount, CAST(0 AS BIT) AS IsUsed, IsActive, IsEarnedReward, RewardRuleId,
                                    ExpirationDate, AllowWithOtherCampaigns, CreatedTime
                                   FROM Coupons 
                                   WHERE UserId IS NULL AND IsActive = 1
                                     AND (ExpirationDate IS NULL OR ExpirationDate > GETUTCDATE())
                                   ORDER BY CreatedTime DESC";

            using var connection = CreateConnection();
            var coupons = await connection.QueryAsync<Coupon>(query);
            return coupons.ToList();
        }

        public async Task<List<Coupon>> GetPublicCouponsAsync(string userId)
        {
            const string query = @"SELECT c.Id, c.Code, c.UserId, c.CouponType, c.Amount, c.Rate, c.MaxDiscountAmount,
                                    c.MinOrderAmount,
                                    CAST(CASE WHEN cu.Id IS NULL THEN 0 ELSE 1 END AS BIT) AS IsUsed,
                                    c.IsActive, c.IsEarnedReward, c.RewardRuleId,
                                    c.ExpirationDate, c.AllowWithOtherCampaigns, c.CreatedTime
                                   FROM Coupons c
                                   LEFT JOIN CouponUsages cu ON cu.CouponId = c.Id AND cu.UserId = @UserId
                                   WHERE c.UserId IS NULL AND c.IsActive = 1
                                     AND (c.ExpirationDate IS NULL OR c.ExpirationDate > GETUTCDATE())
                                   ORDER BY c.CreatedTime DESC";

            using var connection = CreateConnection();
            var coupons = await connection.QueryAsync<Coupon>(query, new { UserId = userId });
            return coupons.ToList();
        }

        public async Task<List<Coupon>> GetByUserIdAsync(string userId)
        {
            const string query = @"SELECT Id, Code, UserId, CouponType, Amount, Rate, MaxDiscountAmount,
                                    MinOrderAmount, IsUsed, IsActive, IsEarnedReward, RewardRuleId,
                                    ExpirationDate, AllowWithOtherCampaigns, CreatedTime
                                   FROM Coupons 
                                   WHERE UserId = @UserId AND IsActive = 1
                                   ORDER BY CreatedTime DESC";

            using var connection = CreateConnection();
            var coupons = await connection.QueryAsync<Coupon>(query, new { UserId = userId });
            return coupons.ToList();
        }

        public async Task<bool> IsCouponUsedByUserAsync(int couponId, string userId)
        {
            const string query = @"SELECT COUNT(1)
                                   FROM CouponUsages
                                   WHERE CouponId = @CouponId AND UserId = @UserId";

            using var connection = CreateConnection();
            var count = await connection.ExecuteScalarAsync<int>(query, new { CouponId = couponId, UserId = userId });
            return count > 0;
        }

        public async Task<bool> SaveAsync(Coupon coupon)
        {
            const string query = @"INSERT INTO Coupons 
                                    (Code, UserId, CouponType, Amount, Rate, MaxDiscountAmount,
                                     MinOrderAmount, IsUsed, IsActive, IsEarnedReward, RewardRuleId, ExpirationDate)
                                   VALUES 
                                    (@Code, @UserId, @CouponType, @Amount, @Rate, @MaxDiscountAmount,
                                     @MinOrderAmount, @IsUsed, @IsActive, @IsEarnedReward, @RewardRuleId, @ExpirationDate)";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, coupon);
            return affectedRows > 0;
        }

        public async Task<bool> UpdateAsync(Coupon coupon)
        {
            const string query = @"UPDATE Coupons SET
                                    Code = @Code,
                                    UserId = @UserId,
                                    CouponType = @CouponType,
                                    Amount = @Amount,
                                    Rate = @Rate,
                                    MaxDiscountAmount = @MaxDiscountAmount,
                                    MinOrderAmount = @MinOrderAmount,
                                    IsUsed = @IsUsed,
                                    IsActive = @IsActive,
                                    IsEarnedReward = @IsEarnedReward,
                                    RewardRuleId = @RewardRuleId,
                                    ExpirationDate = @ExpirationDate
                                   WHERE Id = @Id";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, coupon);
            return affectedRows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string query = "DELETE FROM Coupons WHERE Id = @Id";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }

        public async Task<bool> MarkAsUsedAsync(int id)
        {
            const string query = "UPDATE Coupons SET IsUsed = 1 WHERE Id = @Id";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }

        public async Task<bool> MarkAsUsedAsync(int id, string userId)
        {
            const string query = @"
                IF EXISTS (SELECT 1 FROM Coupons WHERE Id = @Id AND UserId IS NULL)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM CouponUsages WHERE CouponId = @Id AND UserId = @UserId)
                    BEGIN
                        INSERT INTO CouponUsages (CouponId, UserId) VALUES (@Id, @UserId);
                    END
                END
                ELSE
                BEGIN
                    UPDATE Coupons SET IsUsed = 1 WHERE Id = @Id AND UserId = @UserId;
                END";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id, UserId = userId });
            return affectedRows > 0;
        }
    }
}
