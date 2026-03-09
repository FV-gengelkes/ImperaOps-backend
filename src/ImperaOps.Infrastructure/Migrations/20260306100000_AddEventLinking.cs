using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260306100000_AddEventLinking")]
    public partial class AddEventLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventLinkGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_EventLinkGroups", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_EventLinkGroups_ClientId",
                table: "EventLinkGroups",
                column: "ClientId");

            migrationBuilder.CreateTable(
                name: "EventLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<long>(type: "bigint", nullable: false),
                    LinkGroupId = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_EventLinks", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_EventLinks_ClientId_LinkGroupId",
                table: "EventLinks",
                columns: new[] { "ClientId", "LinkGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventLinks_LinkGroupId_EventId",
                table: "EventLinks",
                columns: new[] { "LinkGroupId", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EventLinks");
            migrationBuilder.DropTable(name: "EventLinkGroups");
        }
    }
}
