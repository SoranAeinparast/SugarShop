using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SugarShop.Infrastructure.Persistence.Sales;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    [DbContext(typeof(SugarShopSalesDbContext))]
    [Migration("20261002120000_AddPaymentReviewResolutionAudit")]
    public partial class AddPaymentReviewResolutionAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalRefundNote",
                table: "Payments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRefundReference",
                table: "Payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalRefundedAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRefundedByUserId",
                table: "Payments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationNote",
                table: "Payments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReconciledAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciledByUserId",
                table: "Payments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExternalRefundNote", table: "Payments");
            migrationBuilder.DropColumn(name: "ExternalRefundReference", table: "Payments");
            migrationBuilder.DropColumn(name: "ExternalRefundedAt", table: "Payments");
            migrationBuilder.DropColumn(name: "ExternalRefundedByUserId", table: "Payments");
            migrationBuilder.DropColumn(name: "ReconciliationNote", table: "Payments");
            migrationBuilder.DropColumn(name: "ReconciledAt", table: "Payments");
            migrationBuilder.DropColumn(name: "ReconciledByUserId", table: "Payments");
        }
    }
}
