using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferCategoryType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Categories_CategoryType",
                table: "Categories");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Categories_CategoryType",
                table: "Categories",
                sql: "\"CategoryType\" IN ('Expense', 'Income', 'Both', 'Transfer')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Categories_CategoryType",
                table: "Categories");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Categories_CategoryType",
                table: "Categories",
                sql: "\"CategoryType\" IN ('Expense', 'Income', 'Both')");
        }
    }
}
