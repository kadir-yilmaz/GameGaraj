using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGaraj.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionalLedgerFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserAddresses_UserId_Type",
                schema: "ordering",
                table: "UserAddresses");

            migrationBuilder.DropIndex(
                name: "IX_UserAddresses_UserId_Type_IsDefault",
                schema: "ordering",
                table: "UserAddresses");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DeliveryAddressId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_InvoiceAddressId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "ordering",
                table: "UserAddresses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceAddressId",
                schema: "ordering",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppliedCampaignName",
                schema: "ordering",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CampaignDiscountAmount",
                schema: "ordering",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CouponCode",
                schema: "ordering",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CouponDiscountAmount",
                schema: "ordering",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalTotalAmount",
                schema: "ordering",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFee",
                schema: "ordering",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPaidAmount",
                schema: "ordering",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                schema: "ordering",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                schema: "ordering",
                table: "OrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "OrderPricingLedgers",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPricingLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPricingLedgers_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ordering",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryAddressId",
                schema: "ordering",
                table: "Orders",
                column: "DeliveryAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_InvoiceAddressId",
                schema: "ordering",
                table: "Orders",
                column: "InvoiceAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingLedgers_OrderId",
                schema: "ordering",
                table: "OrderPricingLedgers",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderPricingLedgers",
                schema: "ordering");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DeliveryAddressId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_InvoiceAddressId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AppliedCampaignName",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CampaignDiscountAmount",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponCode",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponDiscountAmount",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OriginalTotalAmount",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingFee",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TotalPaidAmount",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                schema: "ordering",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "ordering",
                table: "OrderItems");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "ordering",
                table: "UserAddresses",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceAddressId",
                schema: "ordering",
                table: "Orders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_UserAddresses_UserId_Type",
                schema: "ordering",
                table: "UserAddresses",
                columns: new[] { "UserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAddresses_UserId_Type_IsDefault",
                schema: "ordering",
                table: "UserAddresses",
                columns: new[] { "UserId", "Type", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryAddressId",
                schema: "ordering",
                table: "Orders",
                column: "DeliveryAddressId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_InvoiceAddressId",
                schema: "ordering",
                table: "Orders",
                column: "InvoiceAddressId",
                unique: true,
                filter: "[InvoiceAddressId] IS NOT NULL");
        }
    }
}
