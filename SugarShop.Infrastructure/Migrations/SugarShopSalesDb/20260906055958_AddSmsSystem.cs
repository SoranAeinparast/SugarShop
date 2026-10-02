using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SugarShop.Infrastructure.Migrations.SugarShopSalesDb
{
    /// <inheritdoc />
    public partial class AddSmsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestockSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SweetItemId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notified = table.Column<bool>(type: "bit", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestockSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsAutoReminderLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReminderType = table.Column<int>(type: "int", nullable: false),
                    RefKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsAutoReminderLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalRecipients = table.Column<int>(type: "int", nullable: false),
                    SentCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsCampaigns", x => x.Id);
                });

            // SmsLogs از قبل در دیتابیس وجود دارد؛ فقط در صورت نبود ساخته می‌شود (idempotent)
            migrationBuilder.Sql(@"
            IF OBJECT_ID(N'[dbo].[SmsLogs]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SmsLogs] (
                    [Id] bigint NOT NULL IDENTITY,
                    [PhoneNumber] nvarchar(900) NOT NULL,
                    [MessageText] nvarchar(max) NOT NULL,
                    [ProviderUsed] int NOT NULL,
                    [Status] int NOT NULL,
                    [ErrorMessage] nvarchar(max) NULL,
                    [Cost] decimal(18,4) NOT NULL,
                    [SentAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_SmsLogs] PRIMARY KEY ([Id])
                );
            END
            ");

            migrationBuilder.CreateTable(
                name: "SmsOtpCodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsOtpCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsSystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SenderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SandboxMode = table.Column<bool>(type: "bit", nullable: false),
                    RespectQuietHours = table.Column<bool>(type: "bit", nullable: false),
                    QuietStartHour = table.Column<int>(type: "int", nullable: false),
                    QuietEndHour = table.Column<int>(type: "int", nullable: false),
                    OtpPerPhonePerHour = table.Column<int>(type: "int", nullable: false),
                    OtpPerIpPerHour = table.Column<int>(type: "int", nullable: false),
                    MaxSmsPerPhonePerDay = table.Column<int>(type: "int", nullable: false),
                    MonthlyBudgetToman = table.Column<int>(type: "int", nullable: false),
                    QueueBatchSize = table.Column<int>(type: "int", nullable: false),
                    QueueDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    OtpLoginEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RegisterWelcomeEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PasswordResetSmsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    StaffAlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NewOrderAlertRoles = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NewOrderAlertOnlyWeighing = table.Column<bool>(type: "bit", nullable: false),
                    NewOrderAlertPhones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CustomCakeAlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CustomCakeAlertPhones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RestockAlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LowStockAlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LowStockThreshold = table.Column<int>(type: "int", nullable: false),
                    LowStockAlertPhones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TicketNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BirthdaySmsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BirthdayDaysBefore = table.Column<int>(type: "int", nullable: false),
                    WinBackEnabled = table.Column<bool>(type: "bit", nullable: false),
                    WinBackDays = table.Column<int>(type: "int", nullable: false),
                    WinBackDiscountPercent = table.Column<int>(type: "int", nullable: false),
                    WinBackDiscountValidityDays = table.Column<int>(type: "int", nullable: false),
                    WinBackMinOrderAmount = table.Column<int>(type: "int", nullable: false),
                    VipPurchaseThresholdToman = table.Column<int>(type: "int", nullable: false),
                    VipAutoSpecialOffers = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsSystemSettings", x => x.Id);
                });

            // SmsTemplates از قبل در دیتابیس وجود دارد؛ فقط در صورت نبود ساخته می‌شود (idempotent)
            migrationBuilder.Sql(@"
            IF OBJECT_ID(N'[dbo].[SmsTemplates]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SmsTemplates] (
                    [Id] int NOT NULL IDENTITY,
                    [Scenario] int NOT NULL,
                    [Title] nvarchar(max) NOT NULL,
                    [BodyText] nvarchar(2000) NOT NULL,
                    [IsActive] bit NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_SmsTemplates] PRIMARY KEY ([Id])
                );
            END
            ");

            migrationBuilder.CreateTable(
                name: "SmsCampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SmsLogId = table.Column<long>(type: "bigint", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsCampaignRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsCampaignRecipients_SmsCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "SmsCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestockSubscriptions_SweetItemId_Notified",
                table: "RestockSubscriptions",
                columns: new[] { "SweetItemId", "Notified" });

            migrationBuilder.CreateIndex(
                name: "IX_RestockSubscriptions_UserId",
                table: "RestockSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsAutoReminderLogs_ReminderType_RefKey",
                table: "SmsAutoReminderLogs",
                columns: new[] { "ReminderType", "RefKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsCampaignRecipients_CampaignId",
                table: "SmsCampaignRecipients",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsCampaigns_Status",
                table: "SmsCampaigns",
                column: "Status");

            // ایندکس‌های SmsLogs — اگر از قبل وجود داشته باشند، دوباره ساخته نمی‌شوند (idempotent)
            migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SmsLogs_PhoneNumber' AND object_id = OBJECT_ID(N'[dbo].[SmsLogs]'))
                CREATE INDEX [IX_SmsLogs_PhoneNumber] ON [dbo].[SmsLogs] ([PhoneNumber]);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SmsLogs_SentAt' AND object_id = OBJECT_ID(N'[dbo].[SmsLogs]'))
                CREATE INDEX [IX_SmsLogs_SentAt] ON [dbo].[SmsLogs] ([SentAt]);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SmsLogs_Status_SentAt' AND object_id = OBJECT_ID(N'[dbo].[SmsLogs]'))
                CREATE INDEX [IX_SmsLogs_Status_SentAt] ON [dbo].[SmsLogs] ([Status], [SentAt]);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_SmsOtpCodes_ExpiresAt",
                table: "SmsOtpCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SmsOtpCodes_Phone_Purpose_IsUsed",
                table: "SmsOtpCodes",
                columns: new[] { "Phone", "Purpose", "IsUsed" });

            migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SmsTemplates_Scenario' AND object_id = OBJECT_ID(N'[dbo].[SmsTemplates]'))
                CREATE INDEX [IX_SmsTemplates_Scenario] ON [dbo].[SmsTemplates] ([Scenario]);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestockSubscriptions");

            migrationBuilder.DropTable(
                name: "SmsAutoReminderLogs");

            migrationBuilder.DropTable(
                name: "SmsCampaignRecipients");

            migrationBuilder.DropTable(
                name: "SmsOtpCodes");

            migrationBuilder.DropTable(
                name: "SmsSystemSettings");

            migrationBuilder.DropTable(
                name: "SmsCampaigns");
        }
    }
}
