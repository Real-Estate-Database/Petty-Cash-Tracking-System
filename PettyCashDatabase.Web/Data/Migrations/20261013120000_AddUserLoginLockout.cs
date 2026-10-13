using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PettyCashDatabase.Web.Data.Migrations
{
    /// <summary>
    /// Adds a temporary, UTC-based login lockout separate from permanent
    /// administrative or integrity locks represented by IsLocked.
    /// </summary>
    [Migration("20261013120000_AddUserLoginLockout")]
    public partial class AddUserLoginLockout : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutUntil",
                table: "Users",
                type: "datetime",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockoutUntil",
                table: "Users");
        }
    }
}
