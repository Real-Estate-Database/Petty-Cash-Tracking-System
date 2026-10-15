using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PettyCashDatabase.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamePasswordResetTokenDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'FailedLoginCount')
                        ALTER TABLE [Users] ADD [FailedLoginCount] int NOT NULL CONSTRAINT [DF_Users_FailedLoginCount_Repair] DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'IsLocked')
                        ALTER TABLE [Users] ADD [IsLocked] bit NOT NULL CONSTRAINT [DF_Users_IsLocked_Repair] DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'IsOrgAdmin')
                        ALTER TABLE [Users] ADD [IsOrgAdmin] bit NOT NULL CONSTRAINT [DF_Users_IsOrgAdmin_Repair] DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LockedDate')
                        ALTER TABLE [Users] ADD [LockedDate] datetime NULL;
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'RowSignature')
                        ALTER TABLE [Users] ADD [RowSignature] nvarchar(500) NULL;
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LockoutUntil')
                        ALTER TABLE [Users] ADD [LockoutUntil] datetime NULL;
                END
            ");

            migrationBuilder.Sql(@"
                DECLARE @constraintName nvarchar(256);
                SELECT @constraintName = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                INNER JOIN sys.tables t ON t.object_id = c.object_id
                WHERE t.name = N'PasswordResetTokens' AND c.name = N'CreatedAt';

                IF @constraintName IS NOT NULL
                    EXEC(N'ALTER TABLE [PasswordResetTokens] DROP CONSTRAINT [' + @constraintName + N']');
            ");

            migrationBuilder.DropIndex(
                name: "IX_PasswordResetTokens_UserId_CreatedAt",
                table: "PasswordResetTokens");

            migrationBuilder.RenameColumn(
                name: "UsedAt",
                table: "PasswordResetTokens",
                newName: "UsedDate");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "PasswordResetTokens",
                newName: "ExpiredDate");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "PasswordResetTokens",
                newName: "EntryDate");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId_EntryDate",
                table: "PasswordResetTokens",
                columns: new[] { "UserId", "EntryDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PasswordResetTokens_UserId_EntryDate",
                table: "PasswordResetTokens");

            migrationBuilder.RenameColumn(
                name: "UsedDate",
                table: "PasswordResetTokens",
                newName: "UsedAt");

            migrationBuilder.RenameColumn(
                name: "ExpiredDate",
                table: "PasswordResetTokens",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "EntryDate",
                table: "PasswordResetTokens",
                newName: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId_CreatedAt",
                table: "PasswordResetTokens",
                columns: new[] { "UserId", "CreatedAt" });
        }
    }
}
