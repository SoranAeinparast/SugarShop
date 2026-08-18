IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Addresses] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [IsDefault] bit NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [FullAddress] nvarchar(2000) NOT NULL,
    [PostalCode] nvarchar(max) NULL,
    [ReceiverName] nvarchar(max) NULL,
    [ReceiverPhone] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Addresses] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Orders] (
    [Id] int NOT NULL IDENTITY,
    [OrderCode] nvarchar(50) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [OrderStatus] int NOT NULL,
    [PaymentStatus] int NOT NULL,
    [TotalAmountSnapshot] decimal(18,2) NOT NULL,
    [DiscountAmountSnapshot] decimal(18,2) NULL,
    [DeliveryFeeSnapshot] decimal(18,2) NOT NULL,
    [TaxAmountSnapshot] decimal(18,2) NULL,
    [CustomerName] nvarchar(max) NOT NULL,
    [CustomerPhone] nvarchar(max) NOT NULL,
    [CustomerAddressId] int NOT NULL,
    [DeliveryDate] datetime2 NULL,
    [DeliveryTime] time NULL,
    [Notes] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Orders_Addresses_CustomerAddressId] FOREIGN KEY ([CustomerAddressId]) REFERENCES [Addresses] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [OrderItems] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [ItemType] int NOT NULL,
    [ProductId] int NULL,
    [SweetItemId] int NULL,
    [Quantity] int NOT NULL,
    [UnitPriceSnapshot] decimal(18,2) NOT NULL,
    [WeightSnapshotGrams] int NULL,
    [TotalPriceSnapshot] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Payments] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [Provider] nvarchar(max) NOT NULL,
    [MerchantRefId] nvarchar(max) NOT NULL,
    [Authority] nvarchar(450) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(max) NOT NULL,
    [PaymentStatus] int NOT NULL,
    [RawRequest] nvarchar(max) NULL,
    [RawResponse] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Payments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_Addresses_UserId_IsDefault] ON [Addresses] ([UserId], [IsDefault]);
GO

CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
GO

CREATE INDEX [IX_Orders_CustomerAddressId] ON [Orders] ([CustomerAddressId]);
GO

CREATE UNIQUE INDEX [IX_Orders_OrderCode] ON [Orders] ([OrderCode]);
GO

CREATE INDEX [IX_Orders_UserId_CreatedAt] ON [Orders] ([UserId], [CreatedAt]);
GO

CREATE INDEX [IX_Payments_Authority] ON [Payments] ([Authority]);
GO

CREATE INDEX [IX_Payments_OrderId] ON [Payments] ([OrderId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260516054210_InitSales', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'UserId');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Orders] ALTER COLUMN [UserId] nvarchar(450) NULL;
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'CustomerAddressId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Orders] ALTER COLUMN [CustomerAddressId] int NULL;
GO

ALTER TABLE [Orders] ADD [CustomerFullAddress] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [Orders] ADD [CustomerPostalCode] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260516160724_GuestOrderFix', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Orders] ADD [AdminNotes] nvarchar(max) NULL;
GO

ALTER TABLE [Orders] ADD [FinalTotalAmount] decimal(18,2) NULL;
GO

ALTER TABLE [Orders] ADD [FinalTotalWeightGrams] int NULL;
GO

ALTER TABLE [Orders] ADD [IsPaymentEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260518192803_AddOrderManagementFields', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [IX_Addresses_UserId_IsDefault] ON [Addresses];
GO

ALTER TABLE [OrderItems] ADD [BoxTitle] nvarchar(max) NULL;
GO

ALTER TABLE [OrderItems] ADD [BoxTypeId] int NULL;
GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'UserId');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [Addresses] ALTER COLUMN [UserId] nvarchar(max) NOT NULL;
GO

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'Title');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [Addresses] ALTER COLUMN [Title] nvarchar(max) NOT NULL;
GO

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'FullAddress');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [Addresses] ALTER COLUMN [FullAddress] nvarchar(max) NOT NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260519081526_AddBoxTypeToOrderItem', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Wallets] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [Balance] decimal(18,2) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Wallets] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [WalletTransactions] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [OrderId] int NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WalletTransactions] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);
GO

CREATE INDEX [IX_Wallets_UserId] ON [Wallets] ([UserId]);
GO

CREATE INDEX [IX_WalletTransactions_OrderId] ON [WalletTransactions] ([OrderId]);
GO

CREATE INDEX [IX_WalletTransactions_UserId] ON [WalletTransactions] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260521133720_AddWalletTables', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Orders] ADD [DiscountAmount] decimal(18,2) NULL;
GO

ALTER TABLE [Orders] ADD [DiscountCodeId] int NULL;
GO

CREATE TABLE [DiscountCodes] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(450) NOT NULL,
    [Description] nvarchar(max) NULL,
    [DiscountType] int NOT NULL,
    [DiscountValue] decimal(18,2) NOT NULL,
    [MinimumOrderAmount] decimal(18,2) NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [UsageLimit] int NULL,
    [UsedCount] int NOT NULL DEFAULT 0,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_DiscountCodes] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_Orders_DiscountCodeId] ON [Orders] ([DiscountCodeId]);
GO

CREATE UNIQUE INDEX [IX_DiscountCodes_Code] ON [DiscountCodes] ([Code]);
GO

ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_DiscountCodes_DiscountCodeId] FOREIGN KEY ([DiscountCodeId]) REFERENCES [DiscountCodes] ([Id]) ON DELETE SET NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260522094850_AddDiscountCode', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Tickets] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [Subject] nvarchar(max) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [AdminResponse] nvarchar(max) NULL,
    [Status] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_Tickets_UserId] ON [Tickets] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260522100814_AddTickets', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [PaymentGatewaySettings] (
    [Id] int NOT NULL IDENTITY,
    [MerchantId] nvarchar(100) NOT NULL,
    [RequestUrl] nvarchar(max) NOT NULL,
    [VerifyUrl] nvarchar(max) NOT NULL,
    [PaymentPageUrl] nvarchar(max) NOT NULL,
    [IsSandbox] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CallbackUrl] nvarchar(max) NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PaymentGatewaySettings] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260522125128_AddPaymentGatewaySetting', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [NewsletterSettings] (
    [Id] int NOT NULL IDENTITY,
    [SmtpHost] nvarchar(200) NOT NULL,
    [SmtpPort] int NOT NULL,
    [EnableSsl] bit NOT NULL,
    [Username] nvarchar(max) NOT NULL,
    [Password] nvarchar(max) NOT NULL,
    [FromEmail] nvarchar(max) NOT NULL,
    [FromName] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_NewsletterSettings] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Subscribers] (
    [Id] int NOT NULL IDENTITY,
    [Email] nvarchar(450) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UnsubscribeToken] nvarchar(max) NULL,
    CONSTRAINT [PK_Subscribers] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [IX_Subscribers_Email] ON [Subscribers] ([Email]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260522150734_AddNewsletterTables', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [SiteSettings] (
    [Id] int NOT NULL IDENTITY,
    [SiteTitle] nvarchar(max) NULL,
    [SiteDescription] nvarchar(max) NULL,
    [LogoPath] nvarchar(500) NULL,
    [FaviconPath] nvarchar(500) NULL,
    [Phone] nvarchar(50) NULL,
    [Email] nvarchar(200) NULL,
    [Address] nvarchar(max) NULL,
    [WorkingHours] nvarchar(max) NULL,
    [InstagramUrl] nvarchar(max) NULL,
    [TelegramUrl] nvarchar(max) NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SiteSettings] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260523192024_AddSiteSettings', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [BoxFinalInfos] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [BoxTitle] nvarchar(450) NOT NULL,
    [FinalWeightGrams] int NOT NULL,
    [FinalPrice] decimal(18,2) NOT NULL,
    [AdminNotes] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_BoxFinalInfos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BoxFinalInfos_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_BoxFinalInfos_BoxTitle] ON [BoxFinalInfos] ([BoxTitle]);
GO

CREATE INDEX [IX_BoxFinalInfos_OrderId] ON [BoxFinalInfos] ([OrderId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260525071213_AddBoxFinalInfo', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260525144024_AddAwaitingReviewStatus', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [MenuItems] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(200) NOT NULL,
    [Url] nvarchar(500) NOT NULL,
    [ParentId] int NULL,
    [Order] int NOT NULL,
    [Location] nvarchar(450) NOT NULL,
    [Icon] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_MenuItems] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ThemeSettings] (
    [Id] int NOT NULL IDENTITY,
    [PrimaryColor] nvarchar(max) NOT NULL,
    [SecondaryColor] nvarchar(max) NOT NULL,
    [HeaderBgColor] nvarchar(max) NOT NULL,
    [HeaderTextColor] nvarchar(max) NOT NULL,
    [FooterBgColor] nvarchar(max) NOT NULL,
    [FooterTextColor] nvarchar(max) NOT NULL,
    [IsHeaderSticky] bit NOT NULL,
    [IsFooterSticky] bit NOT NULL,
    [HeaderTransparency] int NOT NULL,
    [FooterTransparency] int NOT NULL,
    [BodyBgColor] nvarchar(max) NOT NULL,
    [BodyTextColor] nvarchar(max) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ThemeSettings] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_MenuItems_Location] ON [MenuItems] ([Location]);
GO

CREATE INDEX [IX_MenuItems_ParentId] ON [MenuItems] ([ParentId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260526211713_AddThemeAndMenu', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [WalletSettings] (
    [Id] int NOT NULL IDENTITY,
    [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [ReturnType] nvarchar(20) NOT NULL DEFAULT N'Percentage',
    [ReturnValue] decimal(18,2) NOT NULL DEFAULT 0.0,
    [MinimumOrderAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
    [AllowDirectRecharge] bit NOT NULL DEFAULT CAST(1 AS bit),
    [DirectRechargeMinAmount] decimal(18,2) NOT NULL DEFAULT 10000.0,
    [DirectRechargeMaxAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
    [MaxWalletBalance] decimal(18,2) NOT NULL DEFAULT 0.0,
    [ExpiryDays] int NOT NULL DEFAULT 0,
    [UpdatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_WalletSettings] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260528233226_AddWalletSettings', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP TABLE [PaymentGatewaySettings];
GO

CREATE TABLE [PaymentGateways] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(50) NOT NULL,
    [Title] nvarchar(100) NOT NULL,
    [GatewayType] nvarchar(50) NOT NULL,
    [IsActive] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_PaymentGateways] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [PaymentGatewayAccounts] (
    [Id] int NOT NULL IDENTITY,
    [GatewayId] int NOT NULL,
    [Title] nvarchar(100) NOT NULL,
    [IsActive] bit NOT NULL,
    [ConfigData] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_PaymentGatewayAccounts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PaymentGatewayAccounts_PaymentGateways_GatewayId] FOREIGN KEY ([GatewayId]) REFERENCES [PaymentGateways] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_PaymentGatewayAccounts_GatewayId_Title] ON [PaymentGatewayAccounts] ([GatewayId], [Title]);
GO

CREATE UNIQUE INDEX [IX_PaymentGateways_GatewayType] ON [PaymentGateways] ([GatewayType]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260529134335_AddPaymentGatewaysModule', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Payments] ADD [TransactionCode] nvarchar(max) NOT NULL DEFAULT N'';
GO

CREATE INDEX [IX_PaymentGatewayAccounts_GatewayId] ON [PaymentGatewayAccounts] ([GatewayId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260530055201_AddTransactionCodeToPayments', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ThemeSettings] ADD [FooterType] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [ThemeSettings] ADD [HeaderType] int NOT NULL DEFAULT 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260530221709_AddHeaderFooterType', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SiteSettings] ADD [CertificationsJson] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [WhatsAppUrl] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260531154121_AddSocialMediaAndCertifications', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SiteSettings] ADD [AboutShortText] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [FooterCopyrightText] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [QuickLinksJson] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260531161437_AddSiteSettingFooterFields', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [BirthdayReminders] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [Gender] nvarchar(10) NULL,
    [BirthDate] datetime2 NOT NULL,
    [Relation] nvarchar(100) NULL,
    [Email] nvarchar(200) NULL,
    [PhoneNumber] nvarchar(20) NULL,
    [Notes] nvarchar(500) NULL,
    [RemindDaysBefore] int NOT NULL DEFAULT 3,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_BirthdayReminders] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_BirthdayReminders_BirthDate] ON [BirthdayReminders] ([BirthDate]);
GO

CREATE INDEX [IX_BirthdayReminders_UserId] ON [BirthdayReminders] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260604051113_AddBirthdayReminders', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BirthdayReminders]') AND [c].[name] = N'UserId');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [BirthdayReminders] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [BirthdayReminders] ALTER COLUMN [UserId] nvarchar(450) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260604150923_MakeBirthdayReminderUserIdNullable', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260604153522_AddRemainingColumnsToBirthdayReminder', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SiteSettings] ADD [baleUrl] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [eitaaUrl] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [rubikaUrl] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [soroushUrl] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260609171938_AddSocialMediaFieldsToSiteSettings', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [CustomCakeOrders] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Status] int NOT NULL,
    [AdminNotes] nvarchar(1000) NULL,
    [FinalPrice] decimal(18,2) NULL,
    [WeightGrams] int NULL,
    [Flavor] nvarchar(100) NULL,
    [Shape] nvarchar(100) NULL,
    [Ingredients] nvarchar(max) NULL,
    [Occasion] nvarchar(200) NULL,
    [Servings] int NULL,
    [SpecialRequests] nvarchar(2000) NULL,
    [SampleImagePath] nvarchar(500) NULL,
    [PrintImagePath] nvarchar(500) NULL,
    [DesiredDeliveryDateTime] datetime2 NULL,
    CONSTRAINT [PK_CustomCakeOrders] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_CustomCakeOrders_Status] ON [CustomCakeOrders] ([Status]);
GO

CREATE INDEX [IX_CustomCakeOrders_UserId] ON [CustomCakeOrders] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260610180912_AddCustomCakeOrders', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260610181058_AddPrecisionForFinalPrice', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [CustomCakeOrders] ADD [IsPaid] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [CustomCakeOrders] ADD [UpdatedAt] datetime2 NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260611062909_AddIsPaidAndUpdatedAtToCustomCakeOrder', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SiteSettings] ADD [IsGalleryEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260613163453_AddIsGalleryEnabledToSiteSetting', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SiteSettings] ADD [PromoBadgeText] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoButton1Text] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoButton1Url] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoButton2Text] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoButton2Url] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [SiteSettings] ADD [PromoImagePath] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoText] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoTitle] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260614211552_AddPromoBannerFields', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SiteSettings] ADD [PromoBadgeTextColor] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoBgColor] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoButton1BgColor] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoButton1TextColor] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoButton2BgColor] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoButton2TextColor] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoSliderImages] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoTextColor] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [PromoTitleColor] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260615053936_AddPromoSliderAndColors', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SiteSettings] ADD [PromoSpecialEffectEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260615065430_AddPromoSpecialEffect', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ThemeSettings] ADD [BodyDotPatternEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260615194903_AddBodyDotPatternToTheme', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SiteSettings] ADD [ContactFormSubtitle] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [ContactFormTitle] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [ContactPageSubtitle] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [ContactPageTitle] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [ContactSuccessMessage] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [MapEmbedUrl] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [MapLatitude] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [MapLocationName] nvarchar(max) NULL;
GO

ALTER TABLE [SiteSettings] ADD [MapLongitude] nvarchar(max) NULL;
GO

CREATE TABLE [ContactMessages] (
    [Id] int NOT NULL IDENTITY,
    [FullName] nvarchar(100) NOT NULL,
    [Email] nvarchar(200) NOT NULL,
    [Phone] nvarchar(20) NOT NULL,
    [Subject] nvarchar(200) NOT NULL,
    [Message] nvarchar(2000) NOT NULL,
    [IsRead] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ReadAt] datetime2 NULL,
    CONSTRAINT [PK_ContactMessages] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_ContactMessages_CreatedAt] ON [ContactMessages] ([CreatedAt]);
GO

CREATE INDEX [IX_ContactMessages_Email] ON [ContactMessages] ([Email]);
GO

CREATE INDEX [IX_ContactMessages_IsRead] ON [ContactMessages] ([IsRead]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260616172055_AddContactFieldsAndMessages', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [EducationalContents] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [BodyHtml] nvarchar(max) NOT NULL,
    [FeaturedImageUrl] nvarchar(max) NULL,
    [SourceUrl] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [PublishedAt] datetime2 NULL,
    [IsPublished] bit NOT NULL,
    [Category] nvarchar(max) NULL,
    [Tags] nvarchar(max) NULL,
    [MetaDescription] nvarchar(max) NULL,
    CONSTRAINT [PK_EducationalContents] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260618193316_AddEducationalContentTable', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [EducationalContents] ADD [AdminNotes] nvarchar(max) NULL;
GO

ALTER TABLE [EducationalContents] ADD [ApprovedAt] datetime2 NULL;
GO

ALTER TABLE [EducationalContents] ADD [IsApproved] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [EducationalContents] ADD [SourceId] int NULL;
GO

ALTER TABLE [EducationalContents] ADD [TopicId] int NULL;
GO

CREATE TABLE [AIContentSettings] (
    [Id] int NOT NULL IDENTITY,
    [OpenAIApiKey] nvarchar(max) NULL,
    [ModelName] nvarchar(max) NULL,
    [ImageModelName] nvarchar(max) NULL,
    [AutoPublishEnabled] bit NOT NULL,
    [RequireAdminApproval] bit NOT NULL,
    [ScheduleCron] nvarchar(max) NULL,
    [MaxArticlesPerRun] int NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AIContentSettings] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ContentSources] (
    [Id] int NOT NULL IDENTITY,
    [Url] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NULL,
    [Category] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [Priority] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ContentSources] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ContentTopics] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Keywords] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ContentTopics] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260618204006_AddAIContentManagementTables', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO


                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                           WHERE TABLE_NAME = 'ThemeSettings' AND COLUMN_NAME = 'IsHeaderSticky')
                BEGIN
                    ALTER TABLE ThemeSettings DROP COLUMN IsHeaderSticky
                END
            
GO


                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                           WHERE TABLE_NAME = 'ThemeSettings' AND COLUMN_NAME = 'IsFooterSticky')
                BEGIN
                    ALTER TABLE ThemeSettings DROP COLUMN IsFooterSticky
                END
            
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260620203856_RemoveStickyFlagsFinal', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [SplashSettings] (
    [Id] int NOT NULL IDENTITY,
    [VideoPath] nvarchar(max) NOT NULL,
    [ButtonText] nvarchar(max) NOT NULL,
    [IsEnabled] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SplashSettings] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260624131701_AddSplashSettings', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260624135222_UpdateSplashSetting', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [SplashSettings] ADD [IsMuted] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260624140704_AddIsMutedToSplash', N'8.0.16');
GO

COMMIT;
GO

