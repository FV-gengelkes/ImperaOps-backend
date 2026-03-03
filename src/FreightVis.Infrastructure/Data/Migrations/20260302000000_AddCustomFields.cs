using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightVis.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    DataType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Options = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    IncidentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CustomFieldId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Value = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldValues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFields_ClientId",
                table: "CustomFields",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_IncidentId_CustomFieldId",
                table: "CustomFieldValues",
                columns: new[] { "IncidentId", "CustomFieldId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CustomFieldValues");
            migrationBuilder.DropTable(name: "CustomFields");
        }
    }
}
