using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260309000000_AddActiveClientIdToUsers")]
    public partial class AddActiveClientIdToUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ActiveClientId",
                table: "Users",
                type: "bigint",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveClientId",
                table: "Users");
        }
    }
}
