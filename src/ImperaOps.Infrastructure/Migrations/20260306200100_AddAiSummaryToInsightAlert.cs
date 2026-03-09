using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260306200100_AddAiSummaryToInsightAlert")]
    public partial class AddAiSummaryToInsightAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiSummary",
                table: "InsightAlerts",
                type: "longtext",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiSummary",
                table: "InsightAlerts");
        }
    }
}
