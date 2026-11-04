using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddWebullHoldings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalDayProfitLoss",
                table: "WebullAccounts",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalMarketValue",
                table: "WebullAccounts",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalUnrealizedProfitLoss",
                table: "WebullAccounts",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WebullDailyValues",
                columns: table => new
                {
                    WebullConnectionId = table.Column<int>(type: "integer", nullable: false),
                    EasternDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Cash = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MarketValue = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    DayProfitLoss = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    UnrealizedProfitLoss = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebullDailyValues", x => new { x.WebullConnectionId, x.EasternDate, x.AccountId });
                    table.ForeignKey(
                        name: "FK_WebullDailyValues_WebullConnections_WebullConnectionId",
                        column: x => x.WebullConnectionId,
                        principalTable: "WebullConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebullPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WebullAccountId = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InstrumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", nullable: true),
                    LastPrice = table.Column<decimal>(type: "numeric(28,10)", nullable: true),
                    CostPrice = table.Column<decimal>(type: "numeric(28,10)", nullable: true),
                    UnrealizedProfitLoss = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebullPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebullPositions_WebullAccounts_WebullAccountId",
                        column: x => x.WebullAccountId,
                        principalTable: "WebullAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebullPositions_WebullAccountId",
                table: "WebullPositions",
                column: "WebullAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebullDailyValues");

            migrationBuilder.DropTable(
                name: "WebullPositions");

            migrationBuilder.DropColumn(
                name: "TotalDayProfitLoss",
                table: "WebullAccounts");

            migrationBuilder.DropColumn(
                name: "TotalMarketValue",
                table: "WebullAccounts");

            migrationBuilder.DropColumn(
                name: "TotalUnrealizedProfitLoss",
                table: "WebullAccounts");
        }
    }
}
