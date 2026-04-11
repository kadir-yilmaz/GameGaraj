using Dapper;
using Npgsql;

namespace GameGaraj.Discount.API.Services
{
    public static class DbMigrationHelper
    {
        public static void EnsureDatabaseSetup(IConfiguration configuration)
        {
            using var connection = new NpgsqlConnection(configuration.GetConnectionString("PostgreSql"));
            connection.Open();

            // Create table if not exists
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS discount (
                    id SERIAL PRIMARY KEY,
                    user_id VARCHAR(200) NOT NULL,
                    rate INT NOT NULL,
                    code VARCHAR(50) NOT NULL,
                    created_time TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ";
            connection.Execute(createTableSql);

            // Add columns if they don't exist
            var alterTableSql = @"
                ALTER TABLE discount ADD COLUMN IF NOT EXISTS expiration_date TIMESTAMP NULL;
                ALTER TABLE discount ADD COLUMN IF NOT EXISTS allowed_product_ids TEXT NULL;
            ";

            connection.Execute(alterTableSql);
        }
    }
}
