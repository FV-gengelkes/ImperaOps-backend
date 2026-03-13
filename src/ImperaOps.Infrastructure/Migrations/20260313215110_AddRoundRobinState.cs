using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundRobinState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoundRobinStates",
                columns: table => new
                {
                    WorkflowRuleId = table.Column<long>(type: "bigint", nullable: false),
                    LastAssignedIndex = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundRobinStates", x => x.WorkflowRuleId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoundRobinStates");
        }
    }
}
