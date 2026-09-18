using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddStatementLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatementLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastOpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatementLinks_OrderId",
                table: "StatementLinks",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementLinks_Token",
                table: "StatementLinks",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatementLinks");
        }
    }
}
