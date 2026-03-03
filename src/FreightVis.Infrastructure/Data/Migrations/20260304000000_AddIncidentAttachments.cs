using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightVis.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentAttachments",
                columns: table => new
                {
                    Id                    = table.Column<Guid>(type: "char(36)", nullable: false),
                    IncidentId            = table.Column<Guid>(type: "char(36)", nullable: false),
                    ClientId              = table.Column<Guid>(type: "char(36)", nullable: false),
                    FileName              = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    StorageKey            = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    ContentType           = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    FileSizeBytes         = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId      = table.Column<Guid>(type: "char(36)", nullable: true),
                    UploadedByDisplayName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    CreatedAt             = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentAttachments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAttachments_IncidentId_CreatedAt",
                table: "IncidentAttachments",
                columns: new[] { "IncidentId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IncidentAttachments");
        }
    }
}
