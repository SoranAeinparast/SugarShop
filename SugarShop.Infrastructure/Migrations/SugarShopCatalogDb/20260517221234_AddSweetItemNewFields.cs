using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopCatalogDb
{
    /// <inheritdoc />
    public partial class AddSweetItemNewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeightGrams",
                table: "SweetItems");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "SweetItems",
                newName: "PricePerKg");

            migrationBuilder.AddColumn<int>(
                name: "ApproxWeightGrams",
                table: "SweetItems",
                type: "int",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SweetItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "SweetItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "WeightGrams",
                table: "Products",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApproxWeightGrams",
                table: "SweetItems");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SweetItems");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "SweetItems");

            migrationBuilder.RenameColumn(
                name: "PricePerKg",
                table: "SweetItems",
                newName: "Price");

            migrationBuilder.AddColumn<int>(
                name: "WeightGrams",
                table: "SweetItems",
                type: "int",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "WeightGrams",
                table: "Products",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0,
                oldNullable: true);
        }
    }
}
