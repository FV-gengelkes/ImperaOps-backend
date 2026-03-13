using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "WorkflowTransitions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "WorkflowStatuses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "WorkflowRules",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "Tasks",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "SlaRules",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "RootCauseTaxonomyItems",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "Notifications",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "InvestigationWitnesses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "Investigations",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "InvestigationEvidence",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "InsightAlerts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "EventTypes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "Events",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "EventLinks",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "EventLinkGroups",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "DocumentReferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "CustomFieldValues",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "CustomFields",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "ClientWebhooks",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "ClientDocuments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "AuditEvents",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSeedData",
                table: "Attachments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "WorkflowStatuses");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "WorkflowRules");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "SlaRules");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "RootCauseTaxonomyItems");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "InvestigationWitnesses");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "Investigations");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "InvestigationEvidence");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "InsightAlerts");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "EventTypes");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "EventLinks");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "EventLinkGroups");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "DocumentReferences");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "ClientWebhooks");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "ClientDocuments");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IsSeedData",
                table: "Attachments");
        }
    }
}
