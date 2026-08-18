using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddAIContentManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "EducationalContents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "EducationalContents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "EducationalContents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SourceId",
                table: "EducationalContents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TopicId",
                table: "EducationalContents",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AIContentSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OpenAIApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageModelName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AutoPublishEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RequireAdminApproval = table.Column<bool>(type: "bit", nullable: false),
                    ScheduleCron = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxArticlesPerRun = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIContentSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentTopics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Keywords = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTopics", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIContentSettings");

            migrationBuilder.DropTable(
                name: "ContentSources");

            migrationBuilder.DropTable(
                name: "ContentTopics");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "EducationalContents");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "EducationalContents");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "EducationalContents");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "EducationalContents");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "EducationalContents");
        }
    }
}
