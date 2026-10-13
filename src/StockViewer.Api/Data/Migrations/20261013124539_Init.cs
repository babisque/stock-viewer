using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockViewer.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Position",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    ticker = table.Column<string>(type: "TEXT", nullable: false),
                    market = table.Column<string>(type: "TEXT", nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    averagePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    createdAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Position", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Transaction",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    positionId = table.Column<string>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    unitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    fees = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    createdAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transaction", x => x.id);
                    table.ForeignKey(
                        name: "Transaction_positionId_fkey",
                        column: x => x.positionId,
                        principalTable: "Position",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "Position_ticker_market_key",
                table: "Position",
                columns: new[] { "ticker", "market" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Transaction_positionId_date_idx",
                table: "Transaction",
                columns: new[] { "positionId", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transaction");

            migrationBuilder.DropTable(
                name: "Position");
        }
    }
}
