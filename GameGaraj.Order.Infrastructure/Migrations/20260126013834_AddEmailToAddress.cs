using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGaraj.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailToAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "ordering",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                schema: "ordering",
                table: "Addresses");
        }
    }
}
