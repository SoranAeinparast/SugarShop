using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddStoreSettingsAndHardenOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "SmsOtpCodes",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "FreeDeliveryThreshold",
                table: "SiteSettings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // ── داده: غیرفعال‌کردن درگاه‌هایی که به فرآیند پرداخت متصل نیستند ──
            // فقط زیبال در این نسخه پیاده‌سازی شده است؛ فعال‌بودن درگاه دیگر باعث می‌شد ادمین
            // گمان کند پرداخت از آن انجام می‌شود (و در نسخه جدید، شروع پرداخت را متوقف می‌کند).
            migrationBuilder.Sql("UPDATE [PaymentGateways] SET [IsActive] = 0 WHERE [GatewayType] <> 'Zibal'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeDeliveryThreshold",
                table: "SiteSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "SmsOtpCodes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);
        }
    }
}
