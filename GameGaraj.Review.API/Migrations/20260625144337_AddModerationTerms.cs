using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGaraj.Review.API.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModerationTerms",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Term = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationTerms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationTerms_Type_IsActive",
                table: "ModerationTerms",
                columns: new[] { "Type", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationTerms_Type_Term",
                table: "ModerationTerms",
                columns: new[] { "Type", "Term" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModerationTerms");
        }
    }
}
