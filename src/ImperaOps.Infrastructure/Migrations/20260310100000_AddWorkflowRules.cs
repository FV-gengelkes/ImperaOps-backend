using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations;

public partial class AddWorkflowRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WorkflowRules",
            columns: table => new
            {
                Id = table.Column<long>(nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                ClientId = table.Column<long>(nullable: false),
                Name = table.Column<string>(maxLength: 255, nullable: false),
                Description = table.Column<string>(type: "longtext", nullable: true),
                TriggerType = table.Column<string>(maxLength: 50, nullable: false),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                SortOrder = table.Column<int>(nullable: false, defaultValue: 0),
                StopOnMatch = table.Column<bool>(nullable: false, defaultValue: false),
                ConditionsJson = table.Column<string>(type: "longtext", nullable: false),
                ActionsJson = table.Column<string>(type: "longtext", nullable: false),
                CreatedByUserId = table.Column<long>(nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_WorkflowRules", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowRules_ClientId_TriggerType",
            table: "WorkflowRules",
            columns: new[] { "ClientId", "TriggerType" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowRules_DeletedAt",
            table: "WorkflowRules",
            column: "DeletedAt");

        migrationBuilder.CreateTable(
            name: "WorkflowRuleExecutions",
            columns: table => new
            {
                Id = table.Column<long>(nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                WorkflowRuleId = table.Column<long>(nullable: false),
                ClientId = table.Column<long>(nullable: false),
                EventId = table.Column<long>(nullable: false),
                TriggerType = table.Column<string>(maxLength: 50, nullable: false),
                ActionsExecuted = table.Column<int>(nullable: false, defaultValue: 0),
                Success = table.Column<bool>(nullable: false, defaultValue: true),
                ErrorMessage = table.Column<string>(type: "longtext", nullable: true),
                ExecutedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_WorkflowRuleExecutions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowRuleExecutions_WorkflowRuleId",
            table: "WorkflowRuleExecutions",
            column: "WorkflowRuleId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowRuleExecutions_EventId",
            table: "WorkflowRuleExecutions",
            column: "EventId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkflowRuleExecutions_ClientId_ExecutedAt",
            table: "WorkflowRuleExecutions",
            columns: new[] { "ClientId", "ExecutedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WorkflowRuleExecutions");
        migrationBuilder.DropTable(name: "WorkflowRules");
    }
}
