using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260310000000_AddPublicApiSupport")]
    public partial class AddPublicApiSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Client SID ───────────────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "ClientSid",
                table: "Clients",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            // Populate existing clients with SIDs based on their slug
            migrationBuilder.Sql(
                "UPDATE Clients SET ClientSid = CONCAT('csid_', LOWER(HEX(RANDOM_BYTES(8)))) WHERE ClientSid IS NULL;");

            // Now make it non-null and unique
            migrationBuilder.AlterColumn<string>(
                name: "ClientSid",
                table: "Clients",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ClientSid",
                table: "Clients",
                column: "ClientSid",
                unique: true);

            // ── Event source tracking ────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Events",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Events",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedVia",
                table: "Events",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedByApiCredentialId",
                table: "Events",
                type: "bigint",
                nullable: true);

            // Unique constraint for dedupe: (ClientId, Source, ExternalId)
            // MySQL doesn't support partial indexes; use a regular unique index.
            // NULL values are excluded from unique constraints in MySQL, so this
            // naturally handles the case where ExternalId is NULL.
            migrationBuilder.CreateIndex(
                name: "IX_Events_ClientId_Source_ExternalId",
                table: "Events",
                columns: new[] { "ClientId", "Source", "ExternalId" },
                unique: true);

            // ── ApiCredentials ───────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ApiCredentials",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    KeyId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    SecretHash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    SecretLast4 = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false),
                    ScopesJson = table.Column<string>(type: "json", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    LastUsedIp = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    RevokedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCredentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentials_KeyId",
                table: "ApiCredentials",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentials_ClientId_Status",
                table: "ApiCredentials",
                columns: new[] { "ClientId", "Status" });

            // ── ApiRequestLogs ───────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ApiRequestLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<long>(type: "bigint", nullable: true),
                    ApiCredentialId = table.Column<long>(type: "bigint", nullable: true),
                    Method = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    IpAddress = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    RequestId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    LatencyMs = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiRequestLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_ClientId_CreatedAt",
                table: "ApiRequestLogs",
                columns: new[] { "ClientId", "CreatedAt" });

            // ── ApiCredentialAuditLogs ────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ApiCredentialAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    ApiCredentialId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    PerformedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    DetailsJson = table.Column<string>(type: "json", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCredentialAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentialAuditLogs_ApiCredentialId",
                table: "ApiCredentialAuditLogs",
                column: "ApiCredentialId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ApiCredentialAuditLogs");
            migrationBuilder.DropTable(name: "ApiRequestLogs");
            migrationBuilder.DropTable(name: "ApiCredentials");

            migrationBuilder.DropIndex(
                name: "IX_Events_ClientId_Source_ExternalId",
                table: "Events");

            migrationBuilder.DropColumn(name: "CreatedByApiCredentialId", table: "Events");
            migrationBuilder.DropColumn(name: "CreatedVia", table: "Events");
            migrationBuilder.DropColumn(name: "ExternalId", table: "Events");
            migrationBuilder.DropColumn(name: "Source", table: "Events");

            migrationBuilder.DropIndex(name: "IX_Clients_ClientSid", table: "Clients");
            migrationBuilder.DropColumn(name: "ClientSid", table: "Clients");
        }
    }
}
