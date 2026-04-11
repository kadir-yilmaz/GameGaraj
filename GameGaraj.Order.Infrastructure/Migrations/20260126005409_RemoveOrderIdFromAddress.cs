using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGaraj.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrderIdFromAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_Orders_OrderId",
                schema: "ordering",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_OrderId",
                schema: "ordering",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "OrderId",
                schema: "ordering",
                table: "Addresses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                schema: "ordering",
                table: "Addresses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_OrderId",
                schema: "ordering",
                table: "Addresses",
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_Orders_OrderId",
                schema: "ordering",
                table: "Addresses",
                column: "OrderId",
                principalSchema: "ordering",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
