using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;

namespace GameGaraj.Campaign.API.Services.Concrete
{
    public class CouponRewardService : ICouponRewardService
    {
        private readonly string _connectionString;
        private readonly ICouponService _couponService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<CouponRewardService> _logger;

        public CouponRewardService(
            IConfiguration configuration,
            ICouponService couponService,
            INotificationService notificationService,
            ILogger<CouponRewardService> logger)
        {
            _connectionString = configuration.GetConnectionString("SqlServer")!;
            _couponService = couponService;
            _notificationService = notificationService;
            _logger = logger;
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<List<CouponRewardRule>> GetAllAsync()
        {
            const string query = @"SELECT * FROM CouponRewardRules ORDER BY CreatedTime DESC";

            using var connection = CreateConnection();
            var rules = await connection.QueryAsync<CouponRewardRule>(query);
            return rules.ToList();
        }

        public async Task<List<CouponRewardRule>> GetActiveAsync()
        {
            const string query = @"SELECT * FROM CouponRewardRules WHERE IsActive = 1 ORDER BY MinSpendAmount ASC";

            using var connection = CreateConnection();
            var rules = await connection.QueryAsync<CouponRewardRule>(query);
            return rules.ToList();
        }

        public async Task<CouponRewardRule?> GetByIdAsync(int id)
        {
            const string query = @"SELECT * FROM CouponRewardRules WHERE Id = @Id";

            using var connection = CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<CouponRewardRule>(query, new { Id = id });
        }

        public async Task<bool> SaveAsync(CouponRewardRule rule)
        {
            const string query = @"INSERT INTO CouponRewardRules 
                                    (Name, Description, MinSpendAmount, SpendPeriodDays,
                                     RewardCouponType, RewardAmount, RewardRate, RewardMaxDiscount,
                                     RewardMinOrderAmount, RewardValidDays, IsActive)
                                   VALUES 
                                    (@Name, @Description, @MinSpendAmount, @SpendPeriodDays,
                                     @RewardCouponType, @RewardAmount, @RewardRate, @RewardMaxDiscount,
                                     @RewardMinOrderAmount, @RewardValidDays, @IsActive)";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, rule);
            return affectedRows > 0;
        }

        public async Task<bool> UpdateAsync(CouponRewardRule rule)
        {
            const string query = @"UPDATE CouponRewardRules SET
                                    Name = @Name,
                                    Description = @Description,
                                    MinSpendAmount = @MinSpendAmount,
                                    SpendPeriodDays = @SpendPeriodDays,
                                    RewardCouponType = @RewardCouponType,
                                    RewardAmount = @RewardAmount,
                                    RewardRate = @RewardRate,
                                    RewardMaxDiscount = @RewardMaxDiscount,
                                    RewardMinOrderAmount = @RewardMinOrderAmount,
                                    RewardValidDays = @RewardValidDays,
                                    IsActive = @IsActive
                                   WHERE Id = @Id";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, rule);
            return affectedRows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string query = "DELETE FROM CouponRewardRules WHERE Id = @Id";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }

        public async Task AddPurchaseLogAsync(string userId, int orderId, decimal totalAmount)
        {
            const string query = @"INSERT INTO UserPurchaseLogs (UserId, OrderId, TotalAmount, PurchaseDate)
                                   VALUES (@UserId, @OrderId, @TotalAmount, GETUTCDATE())";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(query, new { UserId = userId, OrderId = orderId, TotalAmount = totalAmount });
        }

        public async Task<decimal> GetUserSpendInPeriodAsync(string userId, int days)
        {
            const string query = @"SELECT ISNULL(SUM(TotalAmount), 0) 
                                   FROM UserPurchaseLogs 
                                   WHERE UserId = @UserId 
                                     AND PurchaseDate >= DATEADD(DAY, -@Days, GETUTCDATE())";

            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<decimal>(query, new { UserId = userId, Days = days });
        }

        public async Task<List<Coupon>> CheckAndGrantRewardsAsync(string userId)
        {
            var grantedCoupons = new List<Coupon>();
            var activeRules = await GetActiveAsync();

            if (!activeRules.Any())
                return grantedCoupons;

            foreach (var rule in activeRules)
            {
                try
                {
                    // Kullanıcının bu kural için daha önce kupon kazanıp kazanmadığını kontrol et
                    var alreadyGranted = await HasAlreadyGrantedAsync(userId, rule.Id);
                    if (alreadyGranted)
                        continue;

                    // Belirli süre içindeki toplam alışverişi hesapla
                    var totalSpend = await GetUserSpendInPeriodAsync(userId, rule.SpendPeriodDays);

                    if (totalSpend < rule.MinSpendAmount)
                        continue;

                    // Kural eşleşti! Kupon oluştur
                    var couponCode = GenerateCouponCode(rule);
                    var coupon = new Coupon
                    {
                        Code = couponCode,
                        UserId = userId,
                        CouponType = rule.RewardCouponType,
                        Amount = rule.RewardAmount,
                        Rate = rule.RewardRate,
                        MaxDiscountAmount = rule.RewardMaxDiscount,
                        MinOrderAmount = rule.RewardMinOrderAmount,
                        IsUsed = false,
                        IsActive = true,
                        IsEarnedReward = true,
                        RewardRuleId = rule.Id,
                        ExpirationDate = DateTime.UtcNow.AddDays(rule.RewardValidDays)
                    };

                    var saved = await _couponService.SaveAsync(coupon);
                    if (saved)
                    {
                        grantedCoupons.Add(coupon);

                        // Bildirim oluştur
                        var rewardDescription = rule.RewardCouponType == "FixedAmount"
                            ? $"{rule.RewardAmount:N0} TL"
                            : $"%{rule.RewardRate:N0}";

                        await _notificationService.CreateAsync(new UserNotification
                        {
                            UserId = userId,
                            Title = "Tebrikler! Kupon Kazandınız 🎉",
                            Message = $"{rule.Name} kuralını tamamladınız! {couponCode} kodlu {rewardDescription} indirim kuponunuz {rule.RewardValidDays} gün geçerli.",
                            IconClass = "fas fa-gift",
                            LinkUrl = "/Order/MyCoupons"
                        });

                        _logger.LogInformation(
                            "[CouponRewardService] Kupon verildi — UserId: {UserId}, Kural: {RuleName}, Kupon: {CouponCode}",
                            userId, rule.Name, couponCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CouponRewardService] Ödül kontrolü hatası — Kural: {RuleId}", rule.Id);
                }
            }

            return grantedCoupons;
        }

        private async Task<bool> HasAlreadyGrantedAsync(string userId, int rewardRuleId)
        {
            const string query = @"SELECT COUNT(1) FROM Coupons 
                                   WHERE UserId = @UserId AND RewardRuleId = @RewardRuleId AND IsEarnedReward = 1";

            using var connection = CreateConnection();
            var count = await connection.ExecuteScalarAsync<int>(query, new { UserId = userId, RewardRuleId = rewardRuleId });
            return count > 0;
        }

        private static string GenerateCouponCode(CouponRewardRule rule)
        {
            var prefix = "KAZAN";
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            return $"{prefix}-{suffix}";
        }
    }
}
