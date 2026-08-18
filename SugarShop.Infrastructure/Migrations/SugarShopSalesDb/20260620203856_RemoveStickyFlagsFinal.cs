using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    public partial class RemoveStickyFlagsFinal : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // حذف ستون‌ها با شرط وجود (اگر وجود داشته باشند)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                           WHERE TABLE_NAME = 'ThemeSettings' AND COLUMN_NAME = 'IsHeaderSticky')
                BEGIN
                    ALTER TABLE ThemeSettings DROP COLUMN IsHeaderSticky
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                           WHERE TABLE_NAME = 'ThemeSettings' AND COLUMN_NAME = 'IsFooterSticky')
                BEGIN
                    ALTER TABLE ThemeSettings DROP COLUMN IsFooterSticky
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // بازگرداندن ستون‌ها (در صورت نیاز)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME = 'ThemeSettings' AND COLUMN_NAME = 'IsHeaderSticky')
                BEGIN
                    ALTER TABLE ThemeSettings ADD IsHeaderSticky BIT NOT NULL DEFAULT 1
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME = 'ThemeSettings' AND COLUMN_NAME = 'IsFooterSticky')
                BEGIN
                    ALTER TABLE ThemeSettings ADD IsFooterSticky BIT NOT NULL DEFAULT 0
                END
            ");
        }
    }
}