using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PettyCashDatabase.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingColumnsAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'TimeZoneId')
                BEGIN
                    ALTER TABLE [Users] ADD [TimeZoneId] nvarchar(100) NULL;
                END

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AuditTrail' AND COLUMN_NAME = 'TimeZoneId')
                BEGIN
                    ALTER TABLE [AuditTrail] ADD [TimeZoneId] nvarchar(100) NULL;
                END

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LockoutUntil')
                BEGIN
                    ALTER TABLE [Users] ADD [LockoutUntil] datetime NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'TimeZoneId')
                BEGIN
                    ALTER TABLE [Users] DROP COLUMN [TimeZoneId];
                END

                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AuditTrail' AND COLUMN_NAME = 'TimeZoneId')
                BEGIN
                    ALTER TABLE [AuditTrail] DROP COLUMN [TimeZoneId];
                END

                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LockoutUntil')
                BEGIN
                    ALTER TABLE [Users] DROP COLUMN [LockoutUntil];
                END
            ");
        }
    }
}
