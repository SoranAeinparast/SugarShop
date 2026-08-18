using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddPromoBannerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PromoBadgeText",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoButton1Text",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoButton1Url",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoButton2Text",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoButton2Url",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PromoEnabled",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PromoImagePath",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoText",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromoBadgeText",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoButton1Text",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoButton1Url",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoButton2Text",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoButton2Url",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoImagePath",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoText",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoTitle",
                table: "SiteSettings");
        }
    }
}
