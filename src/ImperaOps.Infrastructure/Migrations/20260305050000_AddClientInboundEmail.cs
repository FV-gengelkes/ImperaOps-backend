using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260305050000_AddClientInboundEmail")]
    public partial class AddClientInboundEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InboundEmailSlug",
                table: "Clients",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DefaultInboundEventTypeId",
                table: "Clients",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DefaultInboundWorkflowStatusId",
                table: "Clients",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_InboundEmailSlug",
                table: "Clients",
                column: "InboundEmailSlug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_InboundEmailSlug",
                table: "Clients");

            migrationBuilder.DropColumn(name: "InboundEmailSlug",                table: "Clients");
            migrationBuilder.DropColumn(name: "DefaultInboundEventTypeId",        table: "Clients");
            migrationBuilder.DropColumn(name: "DefaultInboundWorkflowStatusId",   table: "Clients");
        }
    }
}
