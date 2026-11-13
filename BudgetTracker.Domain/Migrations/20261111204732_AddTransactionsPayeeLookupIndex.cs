using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <summary>
    /// Serves TransactionAccessor.GetLatestCategoryByPayeeAsync (payee -> latest category lookup during Plaid sync).
    /// The query filters on AccountId (via the user's accounts), lower("Payee") = ANY(@keys), CategoryId/Payee NOT NULL,
    /// and picks the newest row per payee by OccurredAt DESC, Id DESC.
    ///
    /// Raw SQL because EF Core cannot model an expression index key (lower("Payee")); the model snapshot is
    /// intentionally unchanged, and EF will not try to drop an index it does not know about.
    ///
    /// Plain CREATE INDEX (not CONCURRENTLY): EF runs each migration inside a transaction together with its
    /// __EFMigrationsHistory insert, and CONCURRENTLY cannot run in a transaction. At personal scale the build takes
    /// well under a second, so the brief SHARE lock (blocks writes, not reads) is acceptable, and keeping it
    /// transactional means a failure rolls back cleanly instead of leaving an INVALID index behind.
    /// If Transactions ever grows large enough for the build to matter, ship a separate migration that runs
    /// CREATE INDEX CONCURRENTLY via migrationBuilder.Sql(..., suppressTransaction: true).
    ///
    /// No INCLUDE ("CategoryId"): Postgres cannot do an index-only scan through the lower("Payee") expression key
    /// (the base "Payee" column is not in the index), so the heap is visited regardless and INCLUDE would only add size.
    /// </summary>
    public partial class AddTransactionsPayeeLookupIndex : Migration
    {
        private const string IndexName = "IX_Transactions_AccountId_LowerPayee_OccurredAt";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                CREATE INDEX IF NOT EXISTS "{IndexName}"
                    ON "Transactions" ("AccountId", lower("Payee"), "OccurredAt" DESC, "Id" DESC)
                    WHERE "CategoryId" IS NOT NULL AND "Payee" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""DROP INDEX IF EXISTS "{IndexName}";""");
        }
    }
}
