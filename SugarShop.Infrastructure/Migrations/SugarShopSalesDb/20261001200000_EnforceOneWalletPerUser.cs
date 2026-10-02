using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SugarShop.Infrastructure.Persistence.Sales;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    [DbContext(typeof(SugarShopSalesDbContext))]
    [Migration("20261001200000_EnforceOneWalletPerUser")]
    public partial class EnforceOneWalletPerUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
;WITH RankedWallets AS
(
    SELECT [Id],
           SUM([Balance]) OVER (PARTITION BY [UserId]) AS [CombinedBalance],
           MAX([UpdatedAt]) OVER (PARTITION BY [UserId]) AS [LatestUpdatedAt],
           ROW_NUMBER() OVER (PARTITION BY [UserId] ORDER BY [Id]) AS [RowNumber]
    FROM [Wallets]
)
UPDATE [w]
SET [Balance] = [r].[CombinedBalance], [UpdatedAt] = [r].[LatestUpdatedAt]
FROM [Wallets] AS [w]
INNER JOIN RankedWallets AS [r] ON [r].[Id] = [w].[Id]
WHERE [r].[RowNumber] = 1;

;WITH RankedWallets AS
(
    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [UserId] ORDER BY [Id]) AS [RowNumber]
    FROM [Wallets]
)
DELETE [w]
FROM [Wallets] AS [w]
INNER JOIN RankedWallets AS [r] ON [r].[Id] = [w].[Id]
WHERE [r].[RowNumber] > 1;");

            migrationBuilder.DropIndex(name: "IX_Wallets_UserId", table: "Wallets");
            migrationBuilder.CreateIndex(
                name: "UX_Wallets_UserId",
                table: "Wallets",
                column: "UserId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "UX_Wallets_UserId", table: "Wallets");
            migrationBuilder.CreateIndex(
                name: "IX_Wallets_UserId",
                table: "Wallets",
                column: "UserId");
        }
    }
}
