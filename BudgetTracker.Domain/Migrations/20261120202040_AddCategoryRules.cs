using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    MatchType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Pattern = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() AT TIME ZONE 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryRules", x => x.Id);
                    table.CheckConstraint("CK_CategoryRules_MatchType", "\"MatchType\" IN ('Contains', 'Exact')");
                    table.CheckConstraint("CK_CategoryRules_Pattern", "btrim(\"Pattern\") <> ''");
                    table.ForeignKey(
                        name: "FK_CategoryRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_CategoryRules_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_CategoryId",
                table: "CategoryRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_UserId",
                table: "CategoryRules",
                column: "UserId");

            // Hand-written: EF Core cannot model an expression index. One rule per pattern and match type per user,
            // ignoring case.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"UQ_CategoryRules_User_Pattern\" ON \"CategoryRules\" (\"UserId\", lower(\"Pattern\"), \"MatchType\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UQ_CategoryRules_User_Pattern\";");

            migrationBuilder.DropTable(
                name: "CategoryRules");
        }
    }
}
