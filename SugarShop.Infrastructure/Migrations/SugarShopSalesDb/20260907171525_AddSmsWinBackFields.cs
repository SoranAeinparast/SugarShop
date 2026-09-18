using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddSmsWinBackFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WinBackCodePrefix",
                table: "SmsSystemSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WinBackCodeTitle",
                table: "SmsSystemSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WinBackCodePrefix",
                table: "SmsSystemSettings");

            migrationBuilder.DropColumn(
                name: "WinBackCodeTitle",
                table: "SmsSystemSettings");
        }
    }
}
