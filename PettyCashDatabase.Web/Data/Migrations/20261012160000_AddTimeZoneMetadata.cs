using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PettyCashDatabase.Web.Data.Migrations
{
    /// <summary>
    /// Stores the selected IANA/Windows time-zone identifier alongside the
    /// legacy numeric offset. The identifier is required for correct DST-aware
    /// conversion; the offset remains for backwards compatibility.
    /// </summary>
    [Migration("20261012160000_AddTimeZoneMetadata")]
    public partial class AddTimeZoneMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "AuditTrail",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "AuditTrail");
        }
    }
}
