using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class RemovePossibleDuplicateFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Transactions_PossibleDuplicateOfId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_PossibleDuplicateOf",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PossibleDuplicateOfId",
                table: "Transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PossibleDuplicateOfId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PossibleDuplicateOf",
                table: "Transactions",
                column: "PossibleDuplicateOfId",
                filter: "[PossibleDuplicateOfId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Transactions_PossibleDuplicateOfId",
                table: "Transactions",
                column: "PossibleDuplicateOfId",
                principalTable: "Transactions",
                principalColumn: "Id");
        }
    }
}
