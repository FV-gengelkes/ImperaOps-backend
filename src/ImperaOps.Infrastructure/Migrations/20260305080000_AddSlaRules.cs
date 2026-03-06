using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260305080000_AddSlaRules")]
    public partial class AddSlaRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SlaRules",
                columns: table => new
                {
                    Id                  = table.Column<long>(type: "bigint", nullable: false)
                                               .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId            = table.Column<long>(type: "bigint", nullable: false),
                    EventTypeId         = table.Column<long>(type: "bigint", nullable: true),
                    Name                = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    InvestigationHours  = table.Column<int>(type: "int", nullable: true),
                    ClosureHours        = table.Column<int>(type: "int", nullable: true),
                    CreatedAt           = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    DeletedAt           = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_SlaRules", x => x.Id))
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SlaRules_ClientId",
                table: "SlaRules",
                column: "ClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SlaRules");
        }
    }
}
