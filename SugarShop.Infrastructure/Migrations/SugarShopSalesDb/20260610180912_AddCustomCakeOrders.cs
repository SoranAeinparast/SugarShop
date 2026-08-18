using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddCustomCakeOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomCakeOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FinalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WeightGrams = table.Column<int>(type: "int", nullable: true),
                    Flavor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Shape = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ingredients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Occasion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Servings = table.Column<int>(type: "int", nullable: true),
                    SpecialRequests = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SampleImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrintImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DesiredDeliveryDateTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomCakeOrders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomCakeOrders_Status",
                table: "CustomCakeOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CustomCakeOrders_UserId",
                table: "CustomCakeOrders",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomCakeOrders");
        }
    }
}
