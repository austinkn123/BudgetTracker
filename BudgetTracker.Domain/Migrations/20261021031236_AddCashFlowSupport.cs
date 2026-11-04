using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddCashFlowSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBalance",
                table: "PlaidAccounts",
                type: "numeric(18,2)",
                nullable: true);

            // Re-type existing rows to the rule PlaidEngine.ClassifyTransactionType now applies to new ones: an Income- or
            // Expense-typed category wins; otherwise only Plaid's INCOME is Income, so refunds and money from friends become
            // positive Expense rows. Transfer and Adjustment rows are left alone; own-account pairs are found on the next sync.
            // Not reverted by Down: the old sign-based types cannot be told apart from deliberate ones afterwards.
            migrationBuilder.Sql("""
                UPDATE "Transactions" AS t
                SET "TransactionType" = CASE
                    WHEN c."CategoryType" = 'Income' THEN 'Income'
                    WHEN c."CategoryType" = 'Expense' THEN 'Expense'
                    WHEN t."PlaidCategoryPrimary" = 'INCOME' THEN 'Income'
                    ELSE 'Expense'
                END
                FROM "Transactions" AS src
                LEFT JOIN "Categories" AS c ON c."Id" = src."CategoryId"
                WHERE src."Id" = t."Id"
                  AND t."TransactionType" IN ('Income', 'Expense');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentBalance",
                table: "PlaidAccounts");
        }
    }
}
