using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260306210000_AddClientStatus")]
    public partial class AddClientStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new Status column with a default of "Active"
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Clients",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            // Migrate existing data: inactive clients get "Inactive"
            migrationBuilder.Sql(
                "UPDATE Clients SET Status = 'Inactive' WHERE IsActive = 0;");

            // Drop the old boolean column
            migrationBuilder.DropColumn(name: "IsActive", table: "Clients");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Clients",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                "UPDATE Clients SET IsActive = CASE WHEN Status = 'Inactive' THEN 0 ELSE 1 END;");

            migrationBuilder.DropColumn(name: "Status", table: "Clients");
        }
    }
}
