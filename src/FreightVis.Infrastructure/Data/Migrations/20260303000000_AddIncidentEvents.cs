using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightVis.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentEvents",
                columns: table => new
                {
                    Id              = table.Column<Guid>(type: "char(36)", nullable: false),
                    IncidentId      = table.Column<Guid>(type: "char(36)", nullable: false),
                    ClientId        = table.Column<Guid>(type: "char(36)", nullable: false),
                    EventType       = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    UserId          = table.Column<Guid>(type: "char(36)", nullable: true),
                    UserDisplayName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    Body            = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt       = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentEvents_IncidentId_CreatedAt",
                table: "IncidentEvents",
                columns: new[] { "IncidentId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IncidentEvents");
        }
    }
}
