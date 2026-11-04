using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddWebullOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebullOrders",
                columns: table => new
                {
                    WebullConnectionId = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InstrumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Side = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FilledQuantity = table.Column<decimal>(type: "numeric(28,10)", nullable: true),
                    FilledPrice = table.Column<decimal>(type: "numeric(28,10)", nullable: true),
                    PlacedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FilledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebullOrders", x => new { x.WebullConnectionId, x.AccountId, x.OrderId });
                    table.ForeignKey(
                        name: "FK_WebullOrders_WebullConnections_WebullConnectionId",
                        column: x => x.WebullConnectionId,
                        principalTable: "WebullConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebullOrderSyncs",
                columns: table => new
                {
                    WebullConnectionId = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CoveredFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CoveredTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    WindowStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    WindowEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaginationKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastError = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebullOrderSyncs", x => new { x.WebullConnectionId, x.AccountId });
                    table.CheckConstraint("CK_WebullOrderSyncs_Covered", "(\"CoveredFrom\" IS NULL AND \"CoveredTo\" IS NULL) OR (\"CoveredFrom\" IS NOT NULL AND \"CoveredTo\" IS NOT NULL)");
                    table.CheckConstraint("CK_WebullOrderSyncs_LastError", "\"LastError\" IN ('Unreachable', 'KeyRejected', 'TokenRejected')");
                    table.CheckConstraint("CK_WebullOrderSyncs_Window", "(\"WindowStart\" IS NULL AND \"WindowEnd\" IS NULL AND \"PaginationKey\" IS NULL) OR (\"WindowStart\" IS NOT NULL AND \"WindowEnd\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_WebullOrderSyncs_WebullConnections_WebullConnectionId",
                        column: x => x.WebullConnectionId,
                        principalTable: "WebullConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebullOrderSyncs");

            migrationBuilder.DropTable(
                name: "WebullOrders");
        }
    }
}
