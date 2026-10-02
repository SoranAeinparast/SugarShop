using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SugarShop.Infrastructure.Persistence.Sales;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    [DbContext(typeof(SugarShopSalesDbContext))]
    [Migration("20261001190000_AddCashbackApplicationMarker")]
    public partial class AddCashbackApplicationMarker : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CashbackAppliedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE [o]
SET [CashbackAppliedAt] = [c].[LastCashbackAt]
FROM [Orders] AS [o]
INNER JOIN
(
    SELECT [OrderId], MAX([CreatedAt]) AS [LastCashbackAt]
    FROM [WalletTransactions]
    WHERE [Type] = N'Cashback' AND [OrderId] IS NOT NULL
    GROUP BY [OrderId]
) AS [c] ON [c].[OrderId] = [o].[Id];");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashbackAppliedAt",
                table: "Orders");
        }
    }
}
