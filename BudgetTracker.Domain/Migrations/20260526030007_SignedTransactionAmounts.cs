using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class SignedTransactionAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BUD-18: switch Transactions.Amount to a signed-amount convention.
            // Expense/Transfer rows become negative; Income stays positive;
            // Adjustment is left as-is and may carry either sign going forward.

            // 1. Drop the old positivity constraint so the UPDATE below can flip signs.
            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_PositiveAmount",
                table: "Transactions");

            // 2. Negate Expense/Transfer rows. The WHERE Amount > 0 guard keeps the
            //    statement idempotent if the migration is re-run against partially
            //    converted data.
            migrationBuilder.Sql(@"
                UPDATE dbo.Transactions
                SET    Amount = -Amount
                WHERE  TransactionType IN ('Expense','Transfer') AND Amount > 0;
            ");

            // 3. Add the new non-zero constraint. EF's AddCheckConstraint emits
            //    WITH CHECK by default, validating every existing row.
            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_NonZeroAmount",
                table: "Transactions",
                sql: "Amount <> 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: drop the non-zero constraint, flip Expense/Transfer rows back
            // to positive, and restore the original positivity constraint.
            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_NonZeroAmount",
                table: "Transactions");

            migrationBuilder.Sql(@"
                UPDATE dbo.Transactions
                SET    Amount = -Amount
                WHERE  TransactionType IN ('Expense','Transfer') AND Amount < 0;
            ");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_PositiveAmount",
                table: "Transactions",
                sql: "Amount > 0");
        }
    }
}
