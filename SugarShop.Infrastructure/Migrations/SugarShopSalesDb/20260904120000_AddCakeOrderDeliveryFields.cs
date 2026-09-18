using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddCakeOrderDeliveryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AddressId",
                table: "CustomCakeOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerFullAddress",
                table: "CustomCakeOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPostalCode",
                table: "CustomCakeOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "CustomCakeOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryMethod",
                table: "CustomCakeOrders",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ReceiverName",
                table: "CustomCakeOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiverPhone",
                table: "CustomCakeOrders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "CustomCakeOrders");

            migrationBuilder.DropColumn(
                name: "CustomerFullAddress",
                table: "CustomCakeOrders");

            migrationBuilder.DropColumn(
                name: "CustomerPostalCode",
                table: "CustomCakeOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "CustomCakeOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryMethod",
                table: "CustomCakeOrders");

            migrationBuilder.DropColumn(
                name: "ReceiverName",
                table: "CustomCakeOrders");

            migrationBuilder.DropColumn(
                name: "ReceiverPhone",
                table: "CustomCakeOrders");
        }
    }
}