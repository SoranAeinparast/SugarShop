using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddWalletSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WalletSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ReturnType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Percentage"),
                    ReturnValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    MinimumOrderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    AllowDirectRecharge = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DirectRechargeMinAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 10000m),
                    DirectRechargeMaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    MaxWalletBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ExpiryDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletSettings");
        }
    }
}
