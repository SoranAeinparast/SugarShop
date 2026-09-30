using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddOrderIsInternalMarker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // پرکردن مقدار ستون نشانگر برای سفارش‌های موجود، با همان قاعده‌ای که پیش‌تر در
            // کوئری‌ها به‌صورت مقایسه‌ی متن Notes نوشته شده بود (بدون حساسیت به بزرگی/کوچکی حروف).
            migrationBuilder.Sql(
                "UPDATE [Orders] SET [IsInternal] = 1 " +
                "WHERE [Notes] = N'WalletRecharge' OR [Notes] LIKE N'CustomCakeOrder[_]%';");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_IsInternal_OrderStatus",
                table: "Orders",
                columns: new[] { "IsInternal", "OrderStatus" })
                .Annotation("SqlServer:Include", new[] { "FinalTotalAmount", "TotalAmountSnapshot" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_IsInternal_OrderStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsInternal",
                table: "Orders");
        }
    }
}
