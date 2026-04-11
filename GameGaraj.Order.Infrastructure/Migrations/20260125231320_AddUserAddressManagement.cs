using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGaraj.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAddressManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Addresses_OrderId",
                schema: "ordering",
                table: "Addresses");

            migrationBuilder.RenameColumn(
                name: "ZipCode",
                schema: "ordering",
                table: "Addresses",
                newName: "PostalCode");

            migrationBuilder.RenameColumn(
                name: "Street",
                schema: "ordering",
                table: "Addresses",
                newName: "PhoneNumber");

            migrationBuilder.RenameColumn(
                name: "Line",
                schema: "ordering",
                table: "Addresses",
                newName: "Neighborhood");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryAddressId",
                schema: "ordering",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceAddressId",
                schema: "ordering",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressDetail",
                schema: "ordering",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "ordering",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "ordering",
                table: "Addresses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "ordering",
                table: "Addresses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UserAddresses",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Province = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    District = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Neighborhood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AddressDetail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAddresses", x => x.Id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_OrderId",
                schema: "ordering",
                table: "Addresses",
                column: "OrderId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Addresses_DeliveryAddressId",
                schema: "ordering",
                table: "Orders",
                column: "DeliveryAddressId",
                principalSchema: "ordering",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Addresses_InvoiceAddressId",
                schema: "ordering",
                table: "Orders",
                column: "InvoiceAddressId",
                principalSchema: "ordering",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Addresses_DeliveryAddressId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Addresses_InvoiceAddressId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "UserAddresses",
                schema: "ordering");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DeliveryAddressId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_InvoiceAddressId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_OrderId",
                schema: "ordering",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "DeliveryAddressId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceAddressId",
                schema: "ordering",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AddressDetail",
                schema: "ordering",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "ordering",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "ordering",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "ordering",
                table: "Addresses");

            migrationBuilder.RenameColumn(
                name: "PostalCode",
                schema: "ordering",
                table: "Addresses",
                newName: "ZipCode");

            migrationBuilder.RenameColumn(
                name: "PhoneNumber",
                schema: "ordering",
                table: "Addresses",
                newName: "Street");

            migrationBuilder.RenameColumn(
                name: "Neighborhood",
                schema: "ordering",
                table: "Addresses",
                newName: "Line");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_OrderId",
                schema: "ordering",
                table: "Addresses",
                column: "OrderId",
                unique: true);
        }
    }
}
