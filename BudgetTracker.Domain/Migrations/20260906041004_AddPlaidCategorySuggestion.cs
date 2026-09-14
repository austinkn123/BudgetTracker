using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaidCategorySuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlaidCategoryPrimary",
                table: "Transactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaidCategoryPrimary",
                table: "Categories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Categories_User_PlaidCategory",
                table: "Categories",
                columns: new[] { "UserId", "PlaidCategoryPrimary" },
                unique: true,
                filter: "[PlaidCategoryPrimary] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Categories_User_PlaidCategory",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "PlaidCategoryPrimary",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PlaidCategoryPrimary",
                table: "Categories");
        }
    }
}
