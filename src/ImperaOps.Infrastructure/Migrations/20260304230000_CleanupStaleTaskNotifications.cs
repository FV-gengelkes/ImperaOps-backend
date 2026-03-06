using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260304230000_CleanupStaleTaskNotifications")]
    public partial class CleanupStaleTaskNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Soft-delete task_assigned notifications that can't be managed:
            // 1. SubEntityPublicId IS NULL  — created before that column existed, no way to link them to a task
            // 2. Referenced task is soft-deleted  — task was deleted after SubEntityPublicId tracking began
            // 3. Referenced task no longer exists  — orphaned reference
            migrationBuilder.Sql(@"
                UPDATE Notifications n
                LEFT JOIN Tasks t ON t.PublicId = n.SubEntityPublicId
                SET n.DeletedAt = UTC_TIMESTAMP(6)
                WHERE n.NotificationType = 'task_assigned'
                  AND n.DeletedAt IS NULL
                  AND (
                      n.SubEntityPublicId IS NULL
                   OR t.Id IS NULL
                   OR t.DeletedAt IS NOT NULL
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot reverse a data cleanup migration
        }
    }
}
