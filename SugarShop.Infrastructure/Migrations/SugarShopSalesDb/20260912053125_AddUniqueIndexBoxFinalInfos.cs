using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddUniqueIndexBoxFinalInfos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // حذف ردیف‌های تکراری (یک سفارش + یک عنوان جعبه) که در اثر دابل‌کلیک دکمه ثبت وزن
            // ایجاد شده‌اند؛ فقط جدیدترین ردیف هر جعبه حفظ می‌شود. بدون این مرحله،
            // ساخت ایندکس یکتا روی دیتابیس‌های دارای تکرار شکست می‌خورد.
            migrationBuilder.Sql(@"
WITH Duplicates AS (
    SELECT [Id],
           ROW_NUMBER() OVER (PARTITION BY [OrderId], [BoxTitle] ORDER BY [Id] DESC) AS [rn]
    FROM [BoxFinalInfos]
)
DELETE FROM [Duplicates] WHERE [rn] > 1;
");

            migrationBuilder.CreateIndex(
                name: "IX_BoxFinalInfos_OrderId_BoxTitle",
                table: "BoxFinalInfos",
                columns: new[] { "OrderId", "BoxTitle" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoxFinalInfos_OrderId_BoxTitle",
                table: "BoxFinalInfos");
        }
    }
}
