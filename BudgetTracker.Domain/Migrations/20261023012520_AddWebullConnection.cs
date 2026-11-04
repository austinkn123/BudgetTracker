using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddWebullConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebullConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AccessTokenEncrypted = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TokenStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TokenCreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastFetchedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebullConnections", x => x.Id);
                    table.CheckConstraint("CK_WebullConnections_LastError", "\"LastError\" IN ('Unreachable', 'KeyRejected', 'TokenRejected')");
                    table.CheckConstraint("CK_WebullConnections_Token", "(\"AccessTokenEncrypted\" IS NULL AND \"TokenStatus\" IS NULL AND \"TokenCreatedAt\" IS NULL) OR (\"AccessTokenEncrypted\" IS NOT NULL AND \"TokenStatus\" IS NOT NULL AND \"TokenCreatedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_WebullConnections_TokenStatus", "\"TokenStatus\" IN ('Pending', 'Normal', 'Expired', 'Invalid')");
                    table.ForeignKey(
                        name: "FK_WebullConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WebullAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WebullConnectionId = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalNetLiquidationValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalCashBalance = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebullAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebullAccounts_WebullConnections_WebullConnectionId",
                        column: x => x.WebullConnectionId,
                        principalTable: "WebullConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_WebullAccounts_WebullConnectionId_AccountId",
                table: "WebullAccounts",
                columns: new[] { "WebullConnectionId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_WebullConnections_UserId",
                table: "WebullConnections",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebullAccounts");

            migrationBuilder.DropTable(
                name: "WebullConnections");
        }
    }
}
