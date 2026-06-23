using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;

namespace GameGaraj.Campaign.API.Services.Concrete
{
    /// <summary>
    /// Dapper ile SQL Server üzerinden kampanya kuralı CRUD operasyonları.
    /// CampaignDb veritabanında CampaignRules tablosu kullanılır.
    /// </summary>
    public class CampaignRuleService : ICampaignRuleService
    {
        private readonly string _connectionString;

        public CampaignRuleService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SqlServer")!;
        }

        private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        private const string SelectColumns = @"Id, Name, Description, RuleType, CategoryId, ProductId,
                                                MinAmount, MinQuantity, FreeQuantity, DiscountRate, FixedDiscount,
                                                BrandName, StartDate, EndDate, ImageUrl,
                                                IsActive, CreatedTime";

        public async Task<List<CampaignRule>> GetAllAsync()
        {
            var query = $"SELECT {SelectColumns} FROM CampaignRules ORDER BY CreatedTime DESC";

            using var connection = CreateConnection();
            var rules = await connection.QueryAsync<CampaignRule>(query);
            return rules.ToList();
        }

        public async Task<List<CampaignRule>> GetActiveAsync()
        {
            var query = $@"SELECT {SelectColumns} FROM CampaignRules 
                           WHERE IsActive = 1 
                             AND (StartDate IS NULL OR StartDate <= GETUTCDATE())
                             AND (EndDate IS NULL OR EndDate >= GETUTCDATE())
                           ORDER BY CreatedTime DESC";

            using var connection = CreateConnection();
            var rules = await connection.QueryAsync<CampaignRule>(query);
            return rules.ToList();
        }

        public async Task<CampaignRule?> GetByIdAsync(int id)
        {
            var query = $"SELECT {SelectColumns} FROM CampaignRules WHERE Id = @Id";

            using var connection = CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<CampaignRule>(query, new { Id = id });
        }

        public async Task<bool> SaveAsync(CampaignRule rule)
        {
            const string query = @"INSERT INTO CampaignRules 
                                    (Name, Description, RuleType, CategoryId, ProductId, MinAmount, 
                                     MinQuantity, FreeQuantity, DiscountRate, FixedDiscount,
                                     BrandName, StartDate, EndDate, ImageUrl, IsActive)
                                   VALUES 
                                    (@Name, @Description, @RuleType, @CategoryId, @ProductId, @MinAmount, 
                                     @MinQuantity, @FreeQuantity, @DiscountRate, @FixedDiscount,
                                     @BrandName, @StartDate, @EndDate, @ImageUrl, @IsActive)";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, rule);
            return affectedRows > 0;
        }

        public async Task<bool> UpdateAsync(CampaignRule rule)
        {
            const string query = @"UPDATE CampaignRules SET
                                    Name = @Name,
                                    Description = @Description,
                                    RuleType = @RuleType,
                                    CategoryId = @CategoryId,
                                    ProductId = @ProductId,
                                    MinAmount = @MinAmount,
                                    MinQuantity = @MinQuantity,
                                    FreeQuantity = @FreeQuantity,
                                    DiscountRate = @DiscountRate,
                                    FixedDiscount = @FixedDiscount,
                                    BrandName = @BrandName,
                                    StartDate = @StartDate,
                                    EndDate = @EndDate,
                                    ImageUrl = @ImageUrl,
                                    IsActive = @IsActive
                                   WHERE Id = @Id";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, rule);
            return affectedRows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string query = "DELETE FROM CampaignRules WHERE Id = @Id";

            using var connection = CreateConnection();
            var affectedRows = await connection.ExecuteAsync(query, new { Id = id });
            return affectedRows > 0;
        }
    }
}
