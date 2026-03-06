using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260304220000_AddSoftDelete")]
    public partial class AddSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var tables = new[]
            {
                "Events", "EventTypes", "WorkflowStatuses", "WorkflowTransitions",
                "Tasks", "AuditEvents", "Attachments", "ClientCounters",
                "Users", "Clients", "UserClientAccess",
                "CustomFields", "CustomFieldValues", "UserTokens",
                "Notifications", "NotificationPreferences"
            };

            foreach (var table in tables)
            {
                migrationBuilder.AddColumn<DateTime>(
                    name: "DeletedAt",
                    table: table,
                    type: "datetime(6)",
                    nullable: true);
            }

            // Indexes on high-traffic tables for query filter performance
            migrationBuilder.CreateIndex(name: "IX_Events_DeletedAt",          table: "Events",        column: "DeletedAt");
            migrationBuilder.CreateIndex(name: "IX_Tasks_DeletedAt",           table: "Tasks",         column: "DeletedAt");
            migrationBuilder.CreateIndex(name: "IX_Notifications_DeletedAt",   table: "Notifications", column: "DeletedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Events_DeletedAt",        table: "Events");
            migrationBuilder.DropIndex(name: "IX_Tasks_DeletedAt",         table: "Tasks");
            migrationBuilder.DropIndex(name: "IX_Notifications_DeletedAt", table: "Notifications");

            var tables = new[]
            {
                "Events", "EventTypes", "WorkflowStatuses", "WorkflowTransitions",
                "Tasks", "AuditEvents", "Attachments", "ClientCounters",
                "Users", "Clients", "UserClientAccess",
                "CustomFields", "CustomFieldValues", "UserTokens",
                "Notifications", "NotificationPreferences"
            };

            foreach (var table in tables)
            {
                migrationBuilder.DropColumn(name: "DeletedAt", table: table);
            }
        }
    }
}
