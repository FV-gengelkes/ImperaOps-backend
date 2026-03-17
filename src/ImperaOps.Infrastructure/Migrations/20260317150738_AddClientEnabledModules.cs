using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientEnabledModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnabledModuleIds",
                table: "Clients",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnabledModuleIds",
                table: "Clients");
        }
    }
}
