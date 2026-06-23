using Dapper;
using Microsoft.Data.SqlClient;

namespace GameGaraj.Campaign.API.Services
{
    /// <summary>
    /// Uygulama ilk başlatıldığında CampaignDb veritabanını ve tüm tabloları oluşturur.
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

            // Şimdi CampaignDb'ye bağlanıp tabloları oluştur
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            // ───── CampaignRules Tablosu ─────
            var createCampaignRulesSql = @"
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

                -- Existing database updates
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CampaignRules') AND name = 'ProductId')
                BEGIN
                    ALTER TABLE CampaignRules ADD ProductId NVARCHAR(100) NULL;
                END;

                -- Ensure Description is nullable
                ALTER TABLE CampaignRules ALTER COLUMN Description NVARCHAR(MAX) NULL;

                -- Yeni alanlar: BrandName, FixedDiscount, StartDate, EndDate, ImageUrl, Priority
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CampaignRules') AND name = 'BrandName')
                BEGIN
                    ALTER TABLE CampaignRules ADD BrandName NVARCHAR(200) NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CampaignRules') AND name = 'FixedDiscount')
                BEGIN
                    ALTER TABLE CampaignRules ADD FixedDiscount DECIMAL(18,2) NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CampaignRules') AND name = 'StartDate')
                BEGIN
                    ALTER TABLE CampaignRules ADD StartDate DATETIME2 NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CampaignRules') AND name = 'EndDate')
                BEGIN
                    ALTER TABLE CampaignRules ADD EndDate DATETIME2 NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CampaignRules') AND name = 'ImageUrl')
                BEGIN
                    ALTER TABLE CampaignRules ADD ImageUrl NVARCHAR(500) NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('CampaignRules') AND name = 'Priority')
                BEGIN
                    ALTER TABLE CampaignRules ADD Priority INT NOT NULL DEFAULT 0;
                END;
            ";
            connection.Execute(createCampaignRulesSql, commandTimeout: 60);

            // ───── ShippingSettings Tablosu ─────
            var createShippingSettingsSql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ShippingSettings' AND xtype='U')
                BEGIN
                    CREATE TABLE ShippingSettings (
                        Id INT PRIMARY KEY,
                        FreeShippingThreshold DECIMAL(18,2) NOT NULL,
                        DefaultShippingFee DECIMAL(18,2) NOT NULL,
                        IsActive BIT NOT NULL DEFAULT 1
                    );

                    IF NOT EXISTS (SELECT 1 FROM ShippingSettings)
                    BEGIN
                        INSERT INTO ShippingSettings (Id, FreeShippingThreshold, DefaultShippingFee, IsActive) 
                        VALUES (1, 1000, 50, 1);
                    END
                END
            ";
            connection.Execute(createShippingSettingsSql, commandTimeout: 60);

            // ───── Coupons Tablosu ─────
            var createCouponsSql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Coupons' AND xtype='U')
                BEGIN
                    CREATE TABLE Coupons (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Code NVARCHAR(100) NOT NULL,
                        UserId NVARCHAR(200) NULL,
                        CouponType NVARCHAR(50) NOT NULL,
                        Amount DECIMAL(18,2) NULL,
                        Rate DECIMAL(5,2) NULL,
                        MaxDiscountAmount DECIMAL(18,2) NULL,
                        MinOrderAmount DECIMAL(18,2) NULL,
                        IsUsed BIT NOT NULL DEFAULT 0,
                        IsActive BIT NOT NULL DEFAULT 1,
                        IsEarnedReward BIT NOT NULL DEFAULT 0,
                        RewardRuleId INT NULL,
                        ExpirationDate DATETIME2 NULL,
                        AllowWithOtherCampaigns BIT NOT NULL DEFAULT 0,
                        CreatedTime DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    )
                END;

                -- Existing database updates
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Coupons') AND name = 'AllowWithOtherCampaigns')
                BEGIN
                    ALTER TABLE Coupons ADD AllowWithOtherCampaigns BIT NOT NULL DEFAULT 0;
                END;
            ";
            connection.Execute(createCouponsSql, commandTimeout: 60);

            // ───── CouponRewardRules Tablosu ─────
            var createCouponRewardRulesSql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CouponRewardRules' AND xtype='U')
                BEGIN
                    CREATE TABLE CouponRewardRules (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(200) NOT NULL,
                        Description NVARCHAR(MAX) NULL,
                        MinSpendAmount DECIMAL(18,2) NOT NULL,
                        SpendPeriodDays INT NOT NULL,
                        RewardCouponType NVARCHAR(50) NOT NULL,
                        RewardAmount DECIMAL(18,2) NULL,
                        RewardRate DECIMAL(5,2) NULL,
                        RewardMaxDiscount DECIMAL(18,2) NULL,
                        RewardMinOrderAmount DECIMAL(18,2) NULL,
                        RewardValidDays INT NOT NULL DEFAULT 30,
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedTime DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    )
                END
            ";
            connection.Execute(createCouponRewardRulesSql, commandTimeout: 60);

            // ───── UserNotifications Tablosu ─────
            var createNotificationsSql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserNotifications' AND xtype='U')
                BEGIN
                    CREATE TABLE UserNotifications (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        UserId NVARCHAR(200) NOT NULL,
                        Title NVARCHAR(300) NOT NULL,
                        Message NVARCHAR(MAX) NOT NULL,
                        IconClass NVARCHAR(100) NULL,
                        LinkUrl NVARCHAR(500) NULL,
                        IsRead BIT NOT NULL DEFAULT 0,
                        CreatedTime DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    )
                END
            ";
            connection.Execute(createNotificationsSql, commandTimeout: 60);

            // ───── UserPurchaseLogs Tablosu ─────
            var createPurchaseLogsSql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserPurchaseLogs' AND xtype='U')
                BEGIN
                    CREATE TABLE UserPurchaseLogs (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        UserId NVARCHAR(200) NOT NULL,
                        OrderId INT NOT NULL,
                        TotalAmount DECIMAL(18,2) NOT NULL,
                        PurchaseDate DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    )
                END
            ";
            connection.Execute(createPurchaseLogsSql, commandTimeout: 60);
        }
    }
}
