using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddPromoSliderAndColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PromoBadgeTextColor",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoBgColor",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoButton1BgColor",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoButton1TextColor",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoButton2BgColor",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoButton2TextColor",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoSliderImages",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoTextColor",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoTitleColor",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromoBadgeTextColor",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoBgColor",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoButton1BgColor",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoButton1TextColor",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoButton2BgColor",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoButton2TextColor",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoSliderImages",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoTextColor",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "PromoTitleColor",
                table: "SiteSettings");
        }
    }
}
