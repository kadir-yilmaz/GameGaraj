using Dapper;
using Npgsql;
using System.Data;
using GameGaraj.Discount.API.Services.Abstract;

namespace GameGaraj.Discount.API.Services.Concrete
{
    public class DiscountService : IDiscountService
    {
        private readonly IDbConnection _dbConnection;

        public DiscountService(IConfiguration configuration)
        {
            _dbConnection = new NpgsqlConnection(
                configuration.GetConnectionString("PostgreSql"));
        }

        public async Task<List<Models.Discount>> GetAllAsync()
        {
            const string query = "SELECT * FROM discount";

            var discounts = await _dbConnection.QueryAsync<Models.Discount>(query);
            return discounts.ToList();
        }

        public async Task<List<Models.Discount>> GetAllByUserIdAsync(string userId)
        {
            const string query = "SELECT * FROM discount WHERE user_id = @UserId";

            var discounts = await _dbConnection.QueryAsync<Models.Discount>(query, new { UserId = userId });
            return discounts.ToList();
        }

        public async Task<Models.Discount?> GetByIdAsync(int id)
        {
            const string query = "SELECT * FROM discount WHERE id = @Id";

            return await _dbConnection.QuerySingleOrDefaultAsync<Models.Discount>(
                query, new { Id = id });
        }

        public async Task<Models.Discount?> GetByCodeAndUserIdAsync(string code, string userId)
        {
            const string query = @"SELECT * 
                                   FROM discount 
                                   WHERE user_id = @UserId AND code = @Code";

            return await _dbConnection.QuerySingleOrDefaultAsync<Models.Discount>(
                query, new { UserId = userId, Code = code });
        }

        public async Task<Models.Discount?> GetByCodeAsync(string code)
        {
            const string query = @"SELECT * 
                                   FROM discount 
                                   WHERE code = @Code";

            return await _dbConnection.QueryFirstOrDefaultAsync<Models.Discount>(
                query, new { Code = code });
        }

        public async Task<bool> SaveAsync(Models.Discount discount)
        {
            const string query = @"INSERT INTO discount (user_id, rate, code, expiration_date, allowed_product_ids)
                                   VALUES (@UserId, @Rate, @Code, @ExpirationDate, @AllowedProductIds)";

            if (discount.ExpirationDate.HasValue)
            {
               discount.ExpirationDate = DateTime.SpecifyKind(discount.ExpirationDate.Value, DateTimeKind.Utc);
            }

            var affectedRows = await _dbConnection.ExecuteAsync(query, discount);
            return affectedRows > 0;
        }

        public async Task<bool> UpdateAsync(Models.Discount discount)
        {
            const string query = @"UPDATE discount 
                                   SET user_id = @UserId,
                                       code = @Code,
                                       rate = @Rate,
                                       expiration_date = @ExpirationDate,
                                       allowed_product_ids = @AllowedProductIds
                                   WHERE id = @Id";

            var affectedRows = await _dbConnection.ExecuteAsync(query, discount);
            return affectedRows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string query = "DELETE FROM discount WHERE id = @Id";

            var affectedRows = await _dbConnection.ExecuteAsync(
                query, new { Id = id });

            return affectedRows > 0;
        }
    }
}
