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
                                    ExpirationDate, CreatedTime
                                   FROM Coupons ORDER BY CreatedTime DESC";

            using var connection = CreateConnection();
            var coupons = await connection.QueryAsync<Coupon>(query);
            return coupons.ToList();
        }

        public async Task<Coupon?> GetByIdAsync(int id)
        {
            const string query = @"SELECT Id, Code, UserId, CouponType, Amount, Rate, MaxDiscountAmount,
                                    MinOrderAmount, IsUsed, IsActive, IsEarnedReward, RewardRuleId,
                                    ExpirationDate, CreatedTime
                                   FROM Coupons WHERE Id = @Id";

            using var connection = CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<Coupon>(query, new { Id = id });
        }

        public async Task<Coupon?> GetByCodeAsync(string code)
        {
            const string query = @"SELECT Id, Code, UserId, CouponType, Amount, Rate, MaxDiscountAmount,
                                    MinOrderAmount, IsUsed, IsActive, IsEarnedReward, RewardRuleId,
                                    ExpirationDate, CreatedTime
                                   FROM Coupons WHERE Code = @Code AND IsActive = 1";

            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<Coupon>(query, new { Code = code });
        }

        public async Task<List<Coupon>> GetPublicCouponsAsync()
        {
            const string query = @"SELECT Id, Code, UserId, CouponType, Amount, Rate, MaxDiscountAmount,
                                    MinOrderAmount, IsUsed, IsActive, IsEarnedReward, RewardRuleId,
                                    ExpirationDate, CreatedTime
                                   FROM Coupons 
                                   WHERE UserId IS NULL AND IsActive = 1 AND IsUsed = 0
                                     AND (ExpirationDate IS NULL OR ExpirationDate > GETUTCDATE())
                                   ORDER BY CreatedTime DESC";

            using var connection = CreateConnection();
            var coupons = await connection.QueryAsync<Coupon>(query);
            return coupons.ToList();
        }

        public async Task<List<Coupon>> GetByUserIdAsync(string userId)
        {
            const string query = @"SELECT Id, Code, UserId, CouponType, Amount, Rate, MaxDiscountAmount,
                                    MinOrderAmount, IsUsed, IsActive, IsEarnedReward, RewardRuleId,
                                    ExpirationDate, CreatedTime
                                   FROM Coupons 
                                   WHERE UserId = @UserId AND IsActive = 1
                                   ORDER BY CreatedTime DESC";

            using var connection = CreateConnection();
            var coupons = await connection.QueryAsync<Coupon>(query, new { UserId = userId });
            return coupons.ToList();
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
    }
}
