using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddAppSettingsToTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AppAndroidEnabled",
                table: "ThemeSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "AppBaseUrl",
                table: "ThemeSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppDisplayName",
                table: "ThemeSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AppEnabled",
                table: "ThemeSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AppIosEnabled",
                table: "ThemeSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppAndroidEnabled",
                table: "ThemeSettings");

            migrationBuilder.DropColumn(
                name: "AppBaseUrl",
                table: "ThemeSettings");

            migrationBuilder.DropColumn(
                name: "AppDisplayName",
                table: "ThemeSettings");

            migrationBuilder.DropColumn(
                name: "AppEnabled",
                table: "ThemeSettings");

            migrationBuilder.DropColumn(
                name: "AppIosEnabled",
                table: "ThemeSettings");
        }
    }
}
