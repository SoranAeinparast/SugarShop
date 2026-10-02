using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SugarShop.Infrastructure.Persistence.Sales;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    [DbContext(typeof(SugarShopSalesDbContext))]
    [Migration("20261001180000_HardenPaymentAttemptsAndRedactSmsLogs")]
    public partial class HardenPaymentAttemptsAndRedactSmsLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve older active attempts for manual settlement checks rather than assuming
            // they were never captured before enforcing the one-active-attempt invariant.
            migrationBuilder.Sql(@"
;WITH RankedAttempts AS
(
    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [OrderId] ORDER BY [CreatedAt] DESC, [Id] DESC) AS [RowNumber]
    FROM [Payments]
    WHERE [PaymentStatus] = 2 AND [Provider] = N'Zibal'
)
UPDATE [p]
SET [PaymentStatus] = 5
FROM [Payments] AS [p]
INNER JOIN RankedAttempts AS [r] ON [r].[Id] = [p].[Id]
WHERE [r].[RowNumber] > 1;

UPDATE [o]
SET [OrderStatus] = 8, [PaymentStatus] = 5, [IsPaymentEnabled] = 0
FROM [Orders] AS [o]
WHERE EXISTS
(
    SELECT 1 FROM [Payments] AS [p]
    WHERE [p].[OrderId] = [o].[Id] AND [p].[Provider] = N'Zibal' AND [p].[PaymentStatus] = 5
);");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "Payments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_OneActiveZibalAttemptPerOrder",
                table: "Payments",
                column: "OrderId",
                unique: true,
                filter: "[PaymentStatus] = 2 AND [Provider] = N'Zibal'");

            // Existing audit rows contained personal data; redact it in place as well.
            migrationBuilder.Sql(@"
UPDATE [SmsLogs]
SET [PhoneNumber] = CASE
        WHEN LEN(ISNULL([PhoneNumber], N'')) >= 4 THEN N'******' + RIGHT([PhoneNumber], 4)
        ELSE N'******'
    END,
    [MessageText] = N'[REDACTED]',
    [ErrorMessage] = CASE WHEN NULLIF([ErrorMessage], N'') IS NULL THEN NULL ELSE N'[REDACTED]' END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Payments_OneActiveZibalAttemptPerOrder",
                table: "Payments");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);
            // Redacted SMS data and de-duplicated active attempts cannot be restored safely.
        }
    }
}
