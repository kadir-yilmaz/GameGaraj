using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGaraj.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailToUserAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "ordering",
                table: "UserAddresses",
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
                table: "UserAddresses");
        }
    }
}
