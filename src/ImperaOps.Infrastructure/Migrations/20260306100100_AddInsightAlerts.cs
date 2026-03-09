using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260306100100_AddInsightAlerts")]
    public partial class AddInsightAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InsightAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    AlertType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "longtext", nullable: false),
                    MetadataJson = table.Column<string>(type: "longtext", nullable: true),
                    RelatedEventIds = table.Column<string>(type: "longtext", nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    AcknowledgedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_InsightAlerts", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_InsightAlerts_ClientId_AlertType_GeneratedAt",
                table: "InsightAlerts",
                columns: new[] { "ClientId", "AlertType", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InsightAlerts_ClientId_IsAcknowledged",
                table: "InsightAlerts",
                columns: new[] { "ClientId", "IsAcknowledged" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InsightAlerts");
        }
    }
}
