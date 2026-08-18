BEGIN TRANSACTION;
GO

CREATE TABLE [BoxTypes] (
    [Id] int NOT NULL IDENTITY,
    [TitleFa] nvarchar(450) NOT NULL,
    [CapacityGrams] int NOT NULL,
    [MaxRows] int NOT NULL,
    [IsActive] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_BoxTypes] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SweetItems] (
    [Id] int NOT NULL IDENTITY,
    [TitleFa] nvarchar(max) NOT NULL,
    [Slug] nvarchar(450) NOT NULL,
    [CategoryId] int NOT NULL,
    [WeightGrams] int NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [InventoryCount] int NOT NULL,
    [IsActive] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SweetItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SweetItems_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_BoxTypes_TitleFa] ON [BoxTypes] ([TitleFa]);
GO

CREATE INDEX [IX_SweetItems_CategoryId] ON [SweetItems] ([CategoryId]);
GO

CREATE INDEX [IX_SweetItems_Slug] ON [SweetItems] ([Slug]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260514075220_BoxAndSweetItems', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260514080240_FixDecimalPrecision', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SweetItems]') AND [c].[name] = N'WeightGrams');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [SweetItems] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [SweetItems] DROP COLUMN [WeightGrams];
GO

EXEC sp_rename N'[SweetItems].[Price]', N'PricePerKg', N'COLUMN';
GO

ALTER TABLE [SweetItems] ADD [ApproxWeightGrams] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [SweetItems] ADD [Description] nvarchar(max) NULL;
GO

ALTER TABLE [SweetItems] ADD [ImagePath] nvarchar(max) NULL;
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'WeightGrams');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Products] ALTER COLUMN [WeightGrams] decimal(18,0) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260517221234_AddSweetItemNewFields', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Categories] ADD [RequiresBoxSelection] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260523201222_AddRequiresBoxSelectionToCategory', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Products] ADD [ImagePath] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260524105701_AddImagePathAndSortOrder', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Sliders] ADD [IsVideo] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260611204932_AddIsVideoToSlider', N'8.0.16');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [GalleryItems] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [MediaType] nvarchar(10) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [ThumbnailPath] nvarchar(500) NULL,
    [VideoDuration] int NULL,
    [SortOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [Category] nvarchar(100) NULL,
    CONSTRAINT [PK_GalleryItems] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_GalleryItems_IsActive] ON [GalleryItems] ([IsActive]);
GO

CREATE INDEX [IX_GalleryItems_SortOrder] ON [GalleryItems] ([SortOrder]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260613163220_AddGalleryTable', N'8.0.16');
GO

COMMIT;
GO

