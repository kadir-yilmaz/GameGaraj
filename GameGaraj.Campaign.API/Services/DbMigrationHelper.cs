using Dapper;
using Microsoft.Data.SqlClient;

namespace GameGaraj.Campaign.API.Services
{
    /// <summary>
    /// Uygulama ilk başlatıldığında CampaignDb veritabanını ve campaign_rule tablosunu oluşturur.
    /// SQL Server üzerinde ayrı bir database olarak çalışır.
    /// </summary>
    public static class DbMigrationHelper
    {
        public static void EnsureDatabaseSetup(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("SqlServer")!;

            // Önce master'a bağlanıp CampaignDb'yi oluştur
            var builder = new SqlConnectionStringBuilder(connectionString);
            var dbName = builder.InitialCatalog;
            builder.InitialCatalog = "master";

            using (var masterConnection = new SqlConnection(builder.ConnectionString))
            {
                masterConnection.Open();

                var createDbSql = $@"
                    IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{dbName}')
                    BEGIN
                        CREATE DATABASE [{dbName}]
                    END";

                masterConnection.Execute(createDbSql);
            }

            // Şimdi CampaignDb'ye bağlanıp tabloyu oluştur
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            var createTableSql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CampaignRules' AND xtype='U')
                BEGIN
                    CREATE TABLE CampaignRules (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(200) NOT NULL,
                        Description NVARCHAR(MAX) NULL,
                        RuleType NVARCHAR(50) NOT NULL,
                        CategoryId NVARCHAR(100) NULL,
                        ProductId NVARCHAR(100) NULL,
                        MinAmount DECIMAL(18,2) NULL,
                        MinQuantity INT NULL,
                        FreeQuantity INT NULL,
                        DiscountRate DECIMAL(5,2) NULL,
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedTime DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    )
                END;

                -- Existing database update
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CampaignRules') AND name = 'ProductId')
                BEGIN
                    ALTER TABLE CampaignRules ADD ProductId NVARCHAR(100) NULL;
                END;

                -- Ensure Description is nullable
                ALTER TABLE CampaignRules ALTER COLUMN Description NVARCHAR(MAX) NULL;

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ShippingSettings' AND xtype='U')
                BEGIN
                    CREATE TABLE ShippingSettings (
                        Id INT PRIMARY KEY,
                        FreeShippingThreshold DECIMAL(18,2) NOT NULL,
                        DefaultShippingFee DECIMAL(18,2) NOT NULL,
                        IsActive BIT NOT NULL DEFAULT 1
                    );

                    -- Başlangıç verisi
                    IF NOT EXISTS (SELECT 1 FROM ShippingSettings)
                    BEGIN
                        INSERT INTO ShippingSettings (Id, FreeShippingThreshold, DefaultShippingFee, IsActive) 
                        VALUES (1, 1000, 50, 1);
                    END
                END
            ";

            connection.Execute(createTableSql, commandTimeout: 60);
        }
    }
}
