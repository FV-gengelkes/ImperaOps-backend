using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using ImperaOps.Infrastructure.Data;

#nullable disable

namespace ImperaOps.Infrastructure.Migrations
{
    [DbContext(typeof(ImperaOpsDbContext))]
    [Migration("20260305070000_AddRootCause")]
    public partial class AddRootCause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RootCauseTaxonomyItems",
                columns: table => new
                {
                    Id         = table.Column<long>(type: "bigint", nullable: false)
                                      .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId   = table.Column<long>(type: "bigint", nullable: false),
                    Name       = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    SortOrder  = table.Column<int>(type: "int", nullable: false),
                    CreatedAt  = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    DeletedAt  = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_RootCauseTaxonomyItems", x => x.Id))
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RootCauseTaxonomyItems_ClientId",
                table: "RootCauseTaxonomyItems",
                column: "ClientId");

            migrationBuilder.AddColumn<long>(
                name: "RootCauseId",
                table: "Events",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectiveAction",
                table: "Events",
                type: "longtext",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_RootCauseId",
                table: "Events",
                column: "RootCauseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_RootCauseTaxonomyItems_RootCauseId",
                table: "Events",
                column: "RootCauseId",
                principalTable: "RootCauseTaxonomyItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_RootCauseTaxonomyItems_RootCauseId",
                table: "Events");

            migrationBuilder.DropIndex(name: "IX_Events_RootCauseId", table: "Events");

            migrationBuilder.DropColumn(name: "RootCauseId",       table: "Events");
            migrationBuilder.DropColumn(name: "CorrectiveAction",   table: "Events");

            migrationBuilder.DropTable(name: "RootCauseTaxonomyItems");
        }
    }
}
