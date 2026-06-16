using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGaraj.Catalog.API.Migrations
{
    /// <inheritdoc />
    public partial class CatalogBestPracticeConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Categories" ("Id", "Name", "Slug", "ParentId", "CreatedAt", "UpdatedAt")
                SELECT 'uncategorized', 'Kategorisiz', 'kategorisiz', NULL, NOW(), NOW()
                WHERE NOT EXISTS (SELECT 1 FROM "Categories" WHERE "Id" = 'uncategorized');

                UPDATE "Products" p
                SET "CategoryId" = 'uncategorized'
                WHERE p."CategoryId" IS NULL
                   OR p."CategoryId" = ''
                   OR NOT EXISTS (SELECT 1 FROM "Categories" c WHERE c."Id" = p."CategoryId");

                UPDATE "CategoryAttributes" a
                SET "CategoryId" = 'uncategorized'
                WHERE a."CategoryId" IS NULL
                   OR a."CategoryId" = ''
                   OR NOT EXISTS (SELECT 1 FROM "Categories" c WHERE c."Id" = a."CategoryId");

                UPDATE "Categories" c
                SET "ParentId" = NULL
                WHERE c."ParentId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "Categories" p WHERE p."Id" = c."ParentId");

                WITH duplicated_products AS (
                    SELECT "Id",
                           "Slug",
                           ROW_NUMBER() OVER (PARTITION BY "Slug" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Products"
                )
                UPDATE "Products" p
                SET "Slug" = duplicated_products."Slug" || '-' || LEFT(duplicated_products."Id", 8)
                FROM duplicated_products
                WHERE p."Id" = duplicated_products."Id"
                  AND duplicated_products.rn > 1;

                WITH duplicated_categories AS (
                    SELECT "Id",
                           "Slug",
                           ROW_NUMBER() OVER (PARTITION BY "Slug" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Categories"
                )
                UPDATE "Categories" c
                SET "Slug" = duplicated_categories."Slug" || '-' || LEFT(duplicated_categories."Id", 8)
                FROM duplicated_categories
                WHERE c."Id" = duplicated_categories."Id"
                  AND duplicated_categories.rn > 1;

                WITH duplicated_attributes AS (
                    SELECT "Id",
                           "Name",
                           ROW_NUMBER() OVER (PARTITION BY "CategoryId", "Name" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "CategoryAttributes"
                )
                UPDATE "CategoryAttributes" a
                SET "Name" = duplicated_attributes."Name" || '_' || LEFT(duplicated_attributes."Id", 8)
                FROM duplicated_attributes
                WHERE a."Id" = duplicated_attributes."Id"
                  AND duplicated_attributes.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Brand",
                table: "Products",
                column: "Brand");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive",
                table: "Products",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsFeatured",
                table: "Products",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Specs",
                table: "Products",
                column: "Specs")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Price_NonNegative",
                table: "Products",
                sql: "\"Price\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ReservedStock_LessOrEqualStock",
                table: "Products",
                sql: "\"ReservedStock\" <= \"Stock\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ReservedStock_NonNegative",
                table: "Products",
                sql: "\"ReservedStock\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Stock_NonNegative",
                table: "Products",
                sql: "\"Stock\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryAttributes_CategoryId_Name",
                table: "CategoryAttributes",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentId",
                table: "Categories",
                column: "ParentId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CategoryAttributes_Categories_CategoryId",
                table: "CategoryAttributes",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_CategoryAttributes_Categories_CategoryId",
                table: "CategoryAttributes");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Brand",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsActive",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsFeatured",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Specs",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Price_NonNegative",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ReservedStock_LessOrEqualStock",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ReservedStock_NonNegative",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Stock_NonNegative",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_CategoryAttributes_CategoryId_Name",
                table: "CategoryAttributes");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Slug",
                table: "Categories");
        }
    }
}
