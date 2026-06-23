using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGaraj.Catalog.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIsShowOnHomeToCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsShowOnHome",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsShowOnHome",
                table: "Categories");
        }
    }
}
