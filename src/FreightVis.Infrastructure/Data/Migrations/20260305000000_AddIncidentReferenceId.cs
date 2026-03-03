using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightVis.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentReferenceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE Incidents ADD COLUMN ReferenceNumber INT NOT NULL DEFAULT 0;");

            migrationBuilder.Sql(@"
UPDATE Incidents i
JOIN (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY ClientId ORDER BY CreatedAt ASC) AS rn
    FROM Incidents
) ranked ON i.Id = ranked.Id
SET i.ReferenceNumber = ranked.rn;");

            migrationBuilder.Sql(
                "ALTER TABLE Incidents ADD UNIQUE INDEX IX_Incidents_ClientId_ReferenceNumber (ClientId, ReferenceNumber);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE Incidents DROP INDEX IX_Incidents_ClientId_ReferenceNumber;");

            migrationBuilder.Sql(
                "ALTER TABLE Incidents DROP COLUMN ReferenceNumber;");
        }
    }
}
