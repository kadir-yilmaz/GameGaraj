using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGaraj.Order.Infrastructure.Migrations
{
    public partial class AddOrderPricingSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
