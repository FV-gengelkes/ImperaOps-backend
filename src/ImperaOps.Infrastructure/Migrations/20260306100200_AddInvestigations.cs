using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260306100200_AddInvestigations")]
    public partial class AddInvestigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Investigations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Summary = table.Column<string>(type: "longtext", nullable: true),
                    RootCauseAnalysis = table.Column<string>(type: "longtext", nullable: true),
                    CorrectiveActions = table.Column<string>(type: "longtext", nullable: true),
                    LeadInvestigatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_Investigations", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_Investigations_EventId",
                table: "Investigations",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Investigations_ClientId_Status",
                table: "Investigations",
                columns: new[] { "ClientId", "Status" });

            migrationBuilder.CreateTable(
                name: "InvestigationWitnesses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    InvestigationId = table.Column<long>(type: "bigint", nullable: false),
                    WitnessName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    WitnessContact = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Statement = table.Column<string>(type: "longtext", nullable: false),
                    StatementDate = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    RecordedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_InvestigationWitnesses", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationWitnesses_InvestigationId_SortOrder",
                table: "InvestigationWitnesses",
                columns: new[] { "InvestigationId", "SortOrder" });

            migrationBuilder.CreateTable(
                name: "InvestigationEvidence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    InvestigationId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: true),
                    EvidenceType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    AttachmentId = table.Column<long>(type: "bigint", nullable: true),
                    CollectedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CollectedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_InvestigationEvidence", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationEvidence_InvestigationId_SortOrder",
                table: "InvestigationEvidence",
                columns: new[] { "InvestigationId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InvestigationEvidence");
            migrationBuilder.DropTable(name: "InvestigationWitnesses");
            migrationBuilder.DropTable(name: "Investigations");
        }
    }
}
