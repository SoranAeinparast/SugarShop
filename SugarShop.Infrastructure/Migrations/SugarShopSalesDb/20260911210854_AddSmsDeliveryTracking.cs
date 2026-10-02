using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddSmsDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "SmsLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryCheckedAt",
                table: "SmsLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DeliveryState",
                table: "SmsLogs",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProviderMessageId",
                table: "SmsLogs",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryCheckedAt",
                table: "SmsLogs");

            migrationBuilder.DropColumn(
                name: "DeliveryState",
                table: "SmsLogs");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                table: "SmsLogs");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "SmsLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
