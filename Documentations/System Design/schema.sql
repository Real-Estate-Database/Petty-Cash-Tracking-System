-- ===============================================================================
-- PETTY CASH & EXPENSE MANAGEMENT SYSTEM
-- PHYSICAL DATABASE DDL SCHEMA SCRIPT (MICROSOFT SQL SERVER 2022)
-- Database-First Clean Architecture
-- Geographic Hierarchy: Countries → Regions → Districts (from RealEstateDatabase)
-- Naming Convention: PascalCase (Title Case) for all table names
-- ===============================================================================

USE [master];
GO

-- Drop existing database for clean re-creation (remove this block after first run)
IF EXISTS (SELECT * FROM sys.databases WHERE name = N'PettyCashDb')
BEGIN
    ALTER DATABASE [PettyCashDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [PettyCashDb];
END;
GO

CREATE DATABASE [PettyCashDb];
GO

USE [PettyCashDb];
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- GEOGRAPHIC LOOKUP TABLES (Sourced from RealEstateDatabase — exact column
-- names, data types, and NULL/NOT NULL preserved from boss's original script)
-- Creation Order: Countries → Regions → Districts (FK dependency chain)
-- ═══════════════════════════════════════════════════════════════════════════════

-- 1. Countries
CREATE TABLE dbo.[Countries] (
    [ID]        [int] IDENTITY(1,1)     NOT NULL,
    [Code]      [nvarchar](255)         NULL,
    [Country]   [nvarchar](255)         NULL,
    [Enabled]   [bit]                   NOT NULL,
    CONSTRAINT [PK_Countries] PRIMARY KEY CLUSTERED ([ID] ASC)
);
GO

-- 2. Regions
CREATE TABLE dbo.[Regions] (
    [RegionID]      [int] IDENTITY(1,1)     NOT NULL,
    [RegionName]    [nvarchar](50)          NULL,
    [Country]       [int]                   NULL,
    [State]         [nvarchar](255)         NULL,
    CONSTRAINT [PK_Regions] PRIMARY KEY CLUSTERED ([RegionID] ASC)
);
GO

-- 3. Districts
CREATE TABLE dbo.[Districts] (
    [DistrictID]    [int] IDENTITY(1,1)     NOT NULL,
    [District]      [nvarchar](50)          NULL,
    [Country]       [int]                   NULL,
    [State]         [nvarchar](255)         NULL,
    [Region]        [int]                   NULL,
    [TempDistrict]  [nvarchar](255)         NULL,
    [Position]      [int]                   NULL,
    CONSTRAINT [PK_Districts] PRIMARY KEY CLUSTERED ([DistrictID] ASC)
);
GO

-- Geographic FK Constraints & Defaults (exact replicas from RealEstateDatabase)
ALTER TABLE dbo.[Districts] ADD CONSTRAINT [DF_Districts_Position] DEFAULT ((0)) FOR [Position];
GO
ALTER TABLE dbo.[Districts] WITH CHECK ADD CONSTRAINT [FK_Districts_Countries] FOREIGN KEY([Country])
    REFERENCES dbo.[Countries] ([ID]);
GO
ALTER TABLE dbo.[Districts] CHECK CONSTRAINT [FK_Districts_Countries];
GO
ALTER TABLE dbo.[Districts] WITH CHECK ADD CONSTRAINT [FK_Districts_Regions] FOREIGN KEY([Region])
    REFERENCES dbo.[Regions] ([RegionID]);
GO
ALTER TABLE dbo.[Districts] CHECK CONSTRAINT [FK_Districts_Regions];
GO
ALTER TABLE dbo.[Regions] WITH CHECK ADD CONSTRAINT [FK_Regions_Countries] FOREIGN KEY([Country])
    REFERENCES dbo.[Countries] ([ID]);
GO
ALTER TABLE dbo.[Regions] CHECK CONSTRAINT [FK_Regions_Countries];
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- CORE BUSINESS TABLES
-- ═══════════════════════════════════════════════════════════════════════════════

-- 4. Organizations
CREATE TABLE dbo.[Organizations] (
    [OrganizationId]    [int] IDENTITY(1,1)     NOT NULL,
    [OrganizationName]  [nvarchar](255)         NOT NULL,
    [Disabled]          [bit]                   NOT NULL,
    [Deleted]           [bit]                   NOT NULL,
    [Logo]              [nvarchar](1000)        NULL,
    [Address]           [nvarchar](500)         NULL,
    [CountryId]         [int]                   NOT NULL,
    [RegionId]          [int]                   NULL,
    [DistrictId]        [int]                   NOT NULL,
    [Email]             [nvarchar](255)         NOT NULL,
    [Telephone]         [nvarchar](50)          NOT NULL,
    [CreatedAt]         [datetime2](7)          NOT NULL,
    [UpdatedAt]         [datetime2](7)          NOT NULL,
    CONSTRAINT [PK_Organizations] PRIMARY KEY CLUSTERED ([OrganizationId] ASC)
);
GO

ALTER TABLE dbo.[Organizations] ADD DEFAULT ((0)) FOR [Disabled];
GO
ALTER TABLE dbo.[Organizations] ADD DEFAULT ((0)) FOR [Deleted];
GO
ALTER TABLE dbo.[Organizations] ADD DEFAULT (GETUTCDATE()) FOR [CreatedAt];
GO
ALTER TABLE dbo.[Organizations] ADD DEFAULT (GETUTCDATE()) FOR [UpdatedAt];
GO

-- 5. Roles
CREATE TABLE dbo.[Roles] (
    [RoleId]            [int] IDENTITY(1,1)     NOT NULL,
    [OrganizationId]    [int]                   NOT NULL,
    [RoleName]          [nvarchar](100)         NOT NULL,
    [Description]       [nvarchar](500)         NOT NULL,
    [IsSystemRole]      [bit]                   NOT NULL,
    [Disabled]          [bit]                   NOT NULL,
    [EntryDate]         [datetime]              NOT NULL,
    [Removed]           [bit]                   NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([RoleId] ASC)
);
GO

ALTER TABLE dbo.[Roles] ADD DEFAULT ((0)) FOR [IsSystemRole];
GO
ALTER TABLE dbo.[Roles] ADD DEFAULT ((0)) FOR [Disabled];
GO
ALTER TABLE dbo.[Roles] ADD DEFAULT (GETUTCDATE()) FOR [EntryDate];
GO
ALTER TABLE dbo.[Roles] ADD DEFAULT ((0)) FOR [Removed];
GO

-- 6. Users
CREATE TABLE dbo.[Users] (
    [UserId]            [int] IDENTITY(1,1)     NOT NULL,
    [OrganizationId]    [int]                   NOT NULL,
    [RoleId]            [int]                   NULL,
    [FirstName]         [nvarchar](50)          NOT NULL,
    [LastName]          [nvarchar](50)          NOT NULL,
    [Email]             [nvarchar](255)         NOT NULL,
    [PasswordHash]      [nvarchar](255)         NOT NULL,
    [TelephoneNumber]   [nvarchar](50)          NOT NULL,
    [IsActive]          [bit]                   NOT NULL,
    [Disabled]          [bit]                   NOT NULL,
    [Deleted]           [bit]                   NOT NULL,
    [Image]             [nvarchar](1000)        NULL,
    [LastLoginDate]     [datetime]              NULL,
    [CountryId]         [int]                   NULL,
    [RegionId]          [int]                   NULL,
    [DistrictId]        [int]                   NOT NULL,
    [EntryDate]         [datetime]              NOT NULL,
    [TimeZone]          [smallint]              NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserId] ASC)
);
GO

ALTER TABLE dbo.[Users] ADD DEFAULT ((1)) FOR [IsActive];
GO
ALTER TABLE dbo.[Users] ADD DEFAULT ((0)) FOR [Disabled];
GO
ALTER TABLE dbo.[Users] ADD DEFAULT ((0)) FOR [Deleted];
GO
ALTER TABLE dbo.[Users] ADD DEFAULT (GETDATE()) FOR [EntryDate];
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- AUTHENTICATION & AUTHORIZATION TABLES
-- ═══════════════════════════════════════════════════════════════════════════════

-- 7. AuditTrail (Combined User Tokens & Audit Trail)
CREATE TABLE dbo.[AuditTrail] (
    [AuditTrailId]       [int] IDENTITY(1,1)     NOT NULL,
    [UserId]             [int]                   NOT NULL,
    [AccessTokenHash]    [nvarchar](500)         NOT NULL,
    [RefreshTokenHash]   [nvarchar](500)         NOT NULL,
    [ClientType]         [nvarchar](100)         NULL,
    [DeviceIdentifier]   [nvarchar](255)         NULL,
    [IpAddress]          [nvarchar](50)          NULL,
    [UserAgent]          [nvarchar](500)         NULL,
    [CountryId]          [int]                   NULL,
    [EntryDate]          [datetime]              NOT NULL,
    [ExpiryDate]         [datetime]              NOT NULL,
    [IsRevoked]          [bit]                   NOT NULL,
    [RevokedDate]        [datetime]              NULL,
    [Description]        [nvarchar](255)         NOT NULL,
    CONSTRAINT [PK_AuditTrail] PRIMARY KEY CLUSTERED ([AuditTrailId] ASC)
);
GO

ALTER TABLE dbo.[AuditTrail] ADD CONSTRAINT [DF_AuditTrail_EntryDate] DEFAULT (GETUTCDATE()) FOR [EntryDate];
GO
ALTER TABLE dbo.[AuditTrail] ADD CONSTRAINT [DF_AuditTrail_IsRevoked] DEFAULT ((0)) FOR [IsRevoked];
GO

-- 8. Permissions
CREATE TABLE dbo.[Permissions] (
    [PermissionId]      [int] IDENTITY(1,1)     NOT NULL,
    [PermissionName]    [nvarchar](100)         NOT NULL,
    [Description]       [nvarchar](500)         NOT NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY CLUSTERED ([PermissionId] ASC)
);
GO

-- 9. UserPermissions (Maps Users to Page-Based Permissions → JWT Claims)
CREATE TABLE dbo.[UserPermissions] (
    [UserPermissionId]  [int] IDENTITY(1,1)     NOT NULL,
    [UserId]            [int]                   NOT NULL,
    [PermissionId]      [int]                   NOT NULL,
    [EntryDate]         [datetime]              NOT NULL,
    CONSTRAINT [PK_UserPermissions] PRIMARY KEY CLUSTERED ([UserPermissionId] ASC)
);
GO

ALTER TABLE dbo.[UserPermissions] ADD DEFAULT (GETUTCDATE()) FOR [EntryDate];
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- TRANSACTION & FINANCIAL TABLES
-- ═══════════════════════════════════════════════════════════════════════════════

-- 10. TransactionTypes
CREATE TABLE dbo.[TransactionTypes] (
    [TransactionTypeId]     [int] IDENTITY(1,1)     NOT NULL,
    [TransactionTypeName]   [nvarchar](100)         NOT NULL,
    [Description]           [nvarchar](500)         NOT NULL,
    [EntryDate]             [datetime]              NOT NULL,
    CONSTRAINT [PK_TransactionTypes] PRIMARY KEY CLUSTERED ([TransactionTypeId] ASC)
);
GO

ALTER TABLE dbo.[TransactionTypes] ADD DEFAULT (GETUTCDATE()) FOR [EntryDate];
GO

-- 11. AdvanceCategories
CREATE TABLE dbo.[AdvanceCategories] (
    [CategoryId]        [int] IDENTITY(1,1)     NOT NULL,
    [OrganizationId]    [int]                   NOT NULL,
    [Name]              [nvarchar](100)         NOT NULL,
    [Description]       [nvarchar](500)         NULL,
    [Disabled]          [bit]                   NOT NULL,
    [EntryDate]         [datetime2](7)          NOT NULL,
    [LastUpdated]       [datetime2](7)          NOT NULL,
    CONSTRAINT [PK_AdvanceCategories] PRIMARY KEY CLUSTERED ([CategoryId] ASC)
);
GO

ALTER TABLE dbo.[AdvanceCategories] ADD DEFAULT ((0)) FOR [Disabled];
GO
ALTER TABLE dbo.[AdvanceCategories] ADD DEFAULT (GETUTCDATE()) FOR [EntryDate];
GO
ALTER TABLE dbo.[AdvanceCategories] ADD DEFAULT (GETUTCDATE()) FOR [LastUpdated];
GO

-- 12. Transactions
CREATE TABLE dbo.[Transactions] (
    [TransactionId]         [int] IDENTITY(1,1)     NOT NULL,
    [OrganizationId]        [int]                   NOT NULL,
    [ReferenceNumber]       [int]                   NOT NULL,
    [TransactionTypeId]     [int]                   NOT NULL,
    [CategoryId]            [int]                   NOT NULL,
    [ParentTransactionId]   [int]                   NULL,
    [UserId]                [int]                   NOT NULL,
    [ApprovedByUserId]      [int]                   NULL,
    [IssuedByUserId]        [int]                   NULL,
    [ReconciledByUserId]    [int]                   NULL,
    [Amount]                [decimal](18, 2)        NOT NULL,
    [Description]           [nvarchar](2000)        NOT NULL,
    [Status]                [nvarchar](50)          NOT NULL,
    [RejectionReason]       [nvarchar](255)         NOT NULL,
    [Notes]                 [nvarchar](2000)        NULL,
    [TransactionDate]       [datetime]              NOT NULL,
    [ApprovedDate]          [datetime]              NULL,
    [ReconciliationDate]    [datetime]              NULL,
    [LastUpdatedDate]       [datetime]              NOT NULL,
    [EntryDate]             [datetime]              NOT NULL,
    CONSTRAINT [PK_Transactions] PRIMARY KEY CLUSTERED ([TransactionId] ASC),
    CONSTRAINT [UQ_Transactions_ReferenceNumber] UNIQUE NONCLUSTERED ([ReferenceNumber] ASC)
);
GO

ALTER TABLE dbo.[Transactions] ADD DEFAULT (SYSUTCDATETIME()) FOR [LastUpdatedDate];
GO
ALTER TABLE dbo.[Transactions] ADD DEFAULT (GETDATE()) FOR [EntryDate];
GO

-- 13. Receipts
CREATE TABLE dbo.[Receipts] (
    [ReceiptId]         [int] IDENTITY(1000,1)  NOT NULL,
    [UserId]            [int]                   NOT NULL,
    [TransactionId]     [int]                   NOT NULL,
    [ReceiptNumber]     [nvarchar](100)         NOT NULL,
    [FileName]          [nvarchar](255)         NOT NULL,
    [ContentType]       [nvarchar](100)         NULL,
    [FileSize]          [int]                   NOT NULL,
    [EntryDate]         [datetime]              NOT NULL,
    [AmountInWords]     [nvarchar](200)         NOT NULL,
    CONSTRAINT [PK_Receipts] PRIMARY KEY CLUSTERED ([ReceiptId] ASC)
);
GO

ALTER TABLE dbo.[Receipts] ADD DEFAULT (GETUTCDATE()) FOR [EntryDate];
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- FOREIGN KEY CONSTRAINTS
-- ═══════════════════════════════════════════════════════════════════════════════

-- Organizations FKs
ALTER TABLE dbo.[Organizations] WITH CHECK ADD CONSTRAINT [FK_Organizations_Countries] FOREIGN KEY([CountryId])
    REFERENCES dbo.[Countries] ([ID]);
GO
ALTER TABLE dbo.[Organizations] CHECK CONSTRAINT [FK_Organizations_Countries];
GO
ALTER TABLE dbo.[Organizations] WITH CHECK ADD CONSTRAINT [FK_Organizations_Regions] FOREIGN KEY([RegionId])
    REFERENCES dbo.[Regions] ([RegionID]);
GO
ALTER TABLE dbo.[Organizations] CHECK CONSTRAINT [FK_Organizations_Regions];
GO
ALTER TABLE dbo.[Organizations] WITH CHECK ADD CONSTRAINT [FK_Organizations_Districts] FOREIGN KEY([DistrictId])
    REFERENCES dbo.[Districts] ([DistrictID]);
GO
ALTER TABLE dbo.[Organizations] CHECK CONSTRAINT [FK_Organizations_Districts];
GO

-- Roles FKs
ALTER TABLE dbo.[Roles] WITH CHECK ADD CONSTRAINT [FK_Roles_Organizations] FOREIGN KEY([OrganizationId])
    REFERENCES dbo.[Organizations] ([OrganizationId]);
GO
ALTER TABLE dbo.[Roles] CHECK CONSTRAINT [FK_Roles_Organizations];
GO

-- Users FKs
ALTER TABLE dbo.[Users] WITH CHECK ADD CONSTRAINT [FK_Users_Organizations] FOREIGN KEY([OrganizationId])
    REFERENCES dbo.[Organizations] ([OrganizationId]);
GO
ALTER TABLE dbo.[Users] CHECK CONSTRAINT [FK_Users_Organizations];
GO
ALTER TABLE dbo.[Users] WITH CHECK ADD CONSTRAINT [FK_Users_Roles] FOREIGN KEY([RoleId])
    REFERENCES dbo.[Roles] ([RoleId]);
GO
ALTER TABLE dbo.[Users] CHECK CONSTRAINT [FK_Users_Roles];
GO
ALTER TABLE dbo.[Users] WITH CHECK ADD CONSTRAINT [FK_Users_Countries] FOREIGN KEY([CountryId])
    REFERENCES dbo.[Countries] ([ID]);
GO
ALTER TABLE dbo.[Users] CHECK CONSTRAINT [FK_Users_Countries];
GO
ALTER TABLE dbo.[Users] WITH CHECK ADD CONSTRAINT [FK_Users_Regions] FOREIGN KEY([RegionId])
    REFERENCES dbo.[Regions] ([RegionID]);
GO
ALTER TABLE dbo.[Users] CHECK CONSTRAINT [FK_Users_Regions];
GO
ALTER TABLE dbo.[Users] WITH CHECK ADD CONSTRAINT [FK_Users_Districts] FOREIGN KEY([DistrictId])
    REFERENCES dbo.[Districts] ([DistrictID]);
GO
ALTER TABLE dbo.[Users] CHECK CONSTRAINT [FK_Users_Districts];
GO

-- AuditTrail FKs
ALTER TABLE dbo.[AuditTrail] WITH CHECK ADD CONSTRAINT [FK_AuditTrail_Countries] FOREIGN KEY([CountryId])
    REFERENCES dbo.[Countries] ([ID]);
GO
ALTER TABLE dbo.[AuditTrail] CHECK CONSTRAINT [FK_AuditTrail_Countries];
GO
ALTER TABLE dbo.[AuditTrail] WITH CHECK ADD CONSTRAINT [FK_AuditTrail_Users] FOREIGN KEY([UserId])
    REFERENCES dbo.[Users] ([UserId]);
GO
ALTER TABLE dbo.[AuditTrail] CHECK CONSTRAINT [FK_AuditTrail_Users];
GO

-- UserPermissions FKs
ALTER TABLE dbo.[UserPermissions] WITH CHECK ADD CONSTRAINT [FK_UserPermissions_Users] FOREIGN KEY([UserId])
    REFERENCES dbo.[Users] ([UserId]);
GO
ALTER TABLE dbo.[UserPermissions] CHECK CONSTRAINT [FK_UserPermissions_Users];
GO
ALTER TABLE dbo.[UserPermissions] WITH CHECK ADD CONSTRAINT [FK_UserPermissions_Permissions] FOREIGN KEY([PermissionId])
    REFERENCES dbo.[Permissions] ([PermissionId]);
GO
ALTER TABLE dbo.[UserPermissions] CHECK CONSTRAINT [FK_UserPermissions_Permissions];
GO

-- AdvanceCategories FKs
ALTER TABLE dbo.[AdvanceCategories] WITH CHECK ADD CONSTRAINT [FK_AdvanceCategories_Organizations] FOREIGN KEY([OrganizationId])
    REFERENCES dbo.[Organizations] ([OrganizationId]);
GO
ALTER TABLE dbo.[AdvanceCategories] CHECK CONSTRAINT [FK_AdvanceCategories_Organizations];
GO

-- Transactions FKs
ALTER TABLE dbo.[Transactions] WITH CHECK ADD CONSTRAINT [FK_Transactions_Organizations] FOREIGN KEY([OrganizationId])
    REFERENCES dbo.[Organizations] ([OrganizationId]);
GO
ALTER TABLE dbo.[Transactions] CHECK CONSTRAINT [FK_Transactions_Organizations];
GO
ALTER TABLE dbo.[Transactions] WITH CHECK ADD CONSTRAINT [FK_Transactions_TransactionTypes] FOREIGN KEY([TransactionTypeId])
    REFERENCES dbo.[TransactionTypes] ([TransactionTypeId]);
GO
ALTER TABLE dbo.[Transactions] CHECK CONSTRAINT [FK_Transactions_TransactionTypes];
GO
ALTER TABLE dbo.[Transactions] WITH CHECK ADD CONSTRAINT [FK_Transactions_AdvanceCategories] FOREIGN KEY([CategoryId])
    REFERENCES dbo.[AdvanceCategories] ([CategoryId]);
GO
ALTER TABLE dbo.[Transactions] CHECK CONSTRAINT [FK_Transactions_AdvanceCategories];
GO
ALTER TABLE dbo.[Transactions] WITH CHECK ADD CONSTRAINT [FK_Transactions_ParentTransaction] FOREIGN KEY([ParentTransactionId])
    REFERENCES dbo.[Transactions] ([TransactionId]);
GO
ALTER TABLE dbo.[Transactions] CHECK CONSTRAINT [FK_Transactions_ParentTransaction];
GO
ALTER TABLE dbo.[Transactions] WITH CHECK ADD CONSTRAINT [FK_Transactions_Users] FOREIGN KEY([UserId])
    REFERENCES dbo.[Users] ([UserId]);
GO
ALTER TABLE dbo.[Transactions] CHECK CONSTRAINT [FK_Transactions_Users];
GO
ALTER TABLE dbo.[Transactions] WITH CHECK ADD CONSTRAINT [FK_Transactions_ApprovedByUser] FOREIGN KEY([ApprovedByUserId])
    REFERENCES dbo.[Users] ([UserId]);
GO
ALTER TABLE dbo.[Transactions] CHECK CONSTRAINT [FK_Transactions_ApprovedByUser];
GO
ALTER TABLE dbo.[Transactions] WITH CHECK ADD CONSTRAINT [FK_Transactions_IssuedByUser] FOREIGN KEY([IssuedByUserId])
    REFERENCES dbo.[Users] ([UserId]);
GO
ALTER TABLE dbo.[Transactions] CHECK CONSTRAINT [FK_Transactions_IssuedByUser];
GO
ALTER TABLE dbo.[Transactions] WITH CHECK ADD CONSTRAINT [FK_Transactions_ReconciledByUser] FOREIGN KEY([ReconciledByUserId])
    REFERENCES dbo.[Users] ([UserId]);
GO
ALTER TABLE dbo.[Transactions] CHECK CONSTRAINT [FK_Transactions_ReconciledByUser];
GO

-- Receipts FKs
ALTER TABLE dbo.[Receipts] WITH CHECK ADD CONSTRAINT [FK_Receipts_Users] FOREIGN KEY([UserId])
    REFERENCES dbo.[Users] ([UserId]);
GO
ALTER TABLE dbo.[Receipts] CHECK CONSTRAINT [FK_Receipts_Users];
GO
ALTER TABLE dbo.[Receipts] WITH CHECK ADD CONSTRAINT [FK_Receipts_Transactions] FOREIGN KEY([TransactionId])
    REFERENCES dbo.[Transactions] ([TransactionId]);
GO
ALTER TABLE dbo.[Receipts] CHECK CONSTRAINT [FK_Receipts_Transactions];
GO
