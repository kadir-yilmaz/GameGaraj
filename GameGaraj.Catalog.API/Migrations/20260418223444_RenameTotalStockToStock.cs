using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGaraj.Catalog.API.Migrations
{
    /// <inheritdoc />
    public partial class RenameTotalStockToStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalStock",
                table: "Products",
                newName: "Stock");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Stock",
                table: "Products",
                newName: "TotalStock");
        }
    }
}
