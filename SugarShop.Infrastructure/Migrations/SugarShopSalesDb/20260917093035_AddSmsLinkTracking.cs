using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddSmsLinkTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmsLinkTrackings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstOpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsLinkTrackings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsLinkTrackings_Kind",
                table: "SmsLinkTrackings",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLinkTrackings_OrderId_Kind",
                table: "SmsLinkTrackings",
                columns: new[] { "OrderId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsLinkTrackings");
        }
    }
}
