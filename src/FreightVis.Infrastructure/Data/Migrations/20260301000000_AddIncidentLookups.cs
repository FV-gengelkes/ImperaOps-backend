using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightVis.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentLookups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FieldKey = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Label = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentLookups", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentLookups_ClientId_FieldKey",
                table: "IncidentLookups",
                columns: new[] { "ClientId", "FieldKey" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentLookups_ClientId_FieldKey_Value",
                table: "IncidentLookups",
                columns: new[] { "ClientId", "FieldKey", "Value" },
                unique: true);

            // Insert the System sentinel client (IsActive=0 so it never appears in UI)
            migrationBuilder.Sql(
                "INSERT INTO Clients (Id, Name, IsActive, CreatedAt) VALUES " +
                "('00000000-0000-0000-0000-000000000000', 'System', 0, UTC_TIMESTAMP());");

            // Seed system incident_type lookups
            migrationBuilder.Sql(
                "INSERT INTO IncidentLookups (Id, ClientId, FieldKey, Label, Value, SortOrder, IsSystem, IsActive, CreatedAt, UpdatedAt) VALUES " +
                "('a1000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000000', 'incident_type', 'Accident',         1, 1, 1, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP())," +
                "('a1000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000000', 'incident_type', 'Injury',           2, 2, 1, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP())," +
                "('a1000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000000', 'incident_type', 'Near Miss',        3, 3, 1, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP())," +
                "('a1000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000000', 'incident_type', 'Property Damage',  4, 4, 1, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP())," +
                "('a1000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000000', 'incident_type', 'Safety Violation', 5, 5, 1, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP());");

            // Seed system status lookups
            migrationBuilder.Sql(
                "INSERT INTO IncidentLookups (Id, ClientId, FieldKey, Label, Value, SortOrder, IsSystem, IsActive, CreatedAt, UpdatedAt) VALUES " +
                "('b1000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000000', 'status', 'Open',        1, 1, 1, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP())," +
                "('b1000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000000', 'status', 'In Progress', 2, 2, 1, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP())," +
                "('b1000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000000', 'status', 'Blocked',     3, 3, 1, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP())," +
                "('b1000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000000', 'status', 'Closed',      4, 4, 1, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM IncidentLookups WHERE ClientId = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(
                "DELETE FROM Clients WHERE Id = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.DropTable(
                name: "IncidentLookups");
        }
    }
}
