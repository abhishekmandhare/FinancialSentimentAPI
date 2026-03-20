using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSymbolSnapshotEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SymbolSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    PreviousScore = table.Column<double>(type: "double precision", nullable: false),
                    Delta = table.Column<double>(type: "double precision", nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Trend = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Dispersion = table.Column<double>(type: "double precision", nullable: false),
                    ArticleCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymbolSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SymbolSnapshots_Symbol",
                table: "SymbolSnapshots",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SymbolSnapshots");
        }
    }
}
