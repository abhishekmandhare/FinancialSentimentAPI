using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackedSymbolsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackedSymbols",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedSymbols", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedSymbols_Symbol",
                table: "TrackedSymbols",
                column: "Symbol",
                unique: true);

            // Seed: copy the 15 symbols that were previously hardcoded in appsettings.json
            // so that existing deployments start tracking the same symbols after migration.
            var seedSymbols = new[]
            {
                "AAPL", "MSFT", "GOOGL", "TSLA", "NVDA",
                "AMZN", "META", "NFLX", "AMD", "INTC",
                "JPM", "BAC", "SPY", "QQQ", "BTC-USD"
            };

            foreach (var symbol in seedSymbols)
            {
                migrationBuilder.InsertData(
                    table: "TrackedSymbols",
                    columns: new[] { "Id", "Symbol", "AddedAt" },
                    values: new object[] { Guid.NewGuid(), symbol, DateTime.UtcNow });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TrackedSymbols");
        }
    }
}
