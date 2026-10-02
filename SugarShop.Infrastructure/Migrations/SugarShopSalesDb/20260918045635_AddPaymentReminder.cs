using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddPaymentReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // پیش‌فرض ستون‌ها عمداً با مقدار پیش‌فرض خودِ مدل یکی است (فعال + ۲۴ ساعت)؛
            // وگرنه نصب‌های موجود پس از این مایگریشن با «غیرفعال/صفر ساعت» بیدار می‌شدند.
            migrationBuilder.AddColumn<int>(
                name: "PaymentReminderDelayHours",
                table: "SmsSystemSettings",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<bool>(
                name: "PaymentReminderEnabled",
                table: "SmsSystemSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentReminderSentAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentReminderDelayHours",
                table: "SmsSystemSettings");

            migrationBuilder.DropColumn(
                name: "PaymentReminderEnabled",
                table: "SmsSystemSettings");

            migrationBuilder.DropColumn(
                name: "PaymentReminderSentAt",
                table: "Orders");
        }
    }
}
