using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CognitoSub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() AT TIME ZONE 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() AT TIME ZONE 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BudgetPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlanMonth = table.Column<DateTime>(type: "date", nullable: false),
                    NetIncomeMonthly = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() AT TIME ZONE 'utc')"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPlans", x => x.Id);
                    table.CheckConstraint("CK_BudgetPlans_NetIncome", "\"NetIncomeMonthly\" >= 0");
                    table.ForeignKey(
                        name: "FK_BudgetPlans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoryType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Expense"),
                    PlaidCategoryPrimary = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Categories_CategoryType", "\"CategoryType\" IN ('Expense', 'Income', 'Both')");
                    table.ForeignKey(
                        name: "FK_Categories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlaidItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PlaidItemId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InstitutionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InstitutionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccessTokenEncrypted = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SyncCursor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ConsentExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() AT TIME ZONE 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaidItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BudgetPlanEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BudgetPlanId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    LineType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Bucket = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Cadence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MonthlyEquivalent = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsStressFactor = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() AT TIME ZONE 'utc')"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPlanEntries", x => x.Id);
                    table.CheckConstraint("CK_BudgetPlanEntries_Amount", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_BudgetPlanEntries_Bucket", "\"Bucket\" IN ('Core', 'Buffer')");
                    table.CheckConstraint("CK_BudgetPlanEntries_Cadence", "\"Cadence\" IN ('Monthly', 'Annual')");
                    table.CheckConstraint("CK_BudgetPlanEntries_LineType", "\"LineType\" IN ('Income', 'Expense')");
                    table.CheckConstraint("CK_BudgetPlanEntries_MonthlyEq", "\"MonthlyEquivalent\" >= 0");
                    table.ForeignKey(
                        name: "FK_BudgetPlanEntries_BudgetPlans_BudgetPlanId",
                        column: x => x.BudgetPlanId,
                        principalTable: "BudgetPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BudgetPlanEntries_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    TransactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Payee = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TransferAccountId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() AT TIME ZONE 'utc')"),
                    PlaidTransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PlaidAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PlaidCategoryPrimary = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsImported = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsPending = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.CheckConstraint("CK_Transactions_NonZeroAmount", "\"Amount\" <> 0");
                    table.CheckConstraint("CK_Transactions_TransactionType", "\"TransactionType\" IN ('Expense', 'Income', 'Transfer', 'Adjustment')");
                    table.CheckConstraint("CK_Transactions_TransferAccount", "(\"TransactionType\" = 'Transfer' AND \"TransferAccountId\" IS NOT NULL) OR (\"TransactionType\" <> 'Transfer' AND \"TransferAccountId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_TransferAccountId",
                        column: x => x.TransferAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Transactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlaidAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlaidItemId = table.Column<int>(type: "integer", nullable: false),
                    PlaidAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Mask = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccountSubtype = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaidAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaidAccounts_PlaidItems_PlaidItemId",
                        column: x => x.PlaidItemId,
                        principalTable: "PlaidItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserId",
                table: "Accounts",
                column: "UserId");

            // Hand-written: EF Core cannot model an expression index. Account names are unique per user ignoring case.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"UQ_Accounts_User_Name\" ON \"Accounts\" (\"UserId\", lower(\"Name\"));");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPlanEntries_CategoryId",
                table: "BudgetPlanEntries",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPlanEntries_Plan_Bucket_Type",
                table: "BudgetPlanEntries",
                columns: new[] { "BudgetPlanId", "Bucket", "LineType" })
                .Annotation("Npgsql:IndexInclude", new[] { "MonthlyEquivalent", "Amount", "Cadence", "CategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPlans_User_Month_Active",
                table: "BudgetPlans",
                columns: new[] { "UserId", "PlanMonth", "IsActive" })
                .Annotation("Npgsql:IndexInclude", new[] { "Name", "NetIncomeMonthly" });

            // Hand-written: EF Core cannot model an expression index. Plan names are unique per user and month ignoring case.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"UQ_BudgetPlans_User_Month_Name\" ON \"BudgetPlans\" (\"UserId\", \"PlanMonth\", lower(\"Name\"));");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId",
                table: "Categories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_Categories_User_PlaidCategory",
                table: "Categories",
                columns: new[] { "UserId", "PlaidCategoryPrimary" },
                unique: true,
                filter: "\"PlaidCategoryPrimary\" IS NOT NULL");

            // Hand-written: EF Core cannot model an expression index. Category names are unique per user ignoring case.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"UQ_Categories_User_Name\" ON \"Categories\" (\"UserId\", lower(\"Name\"));");

            migrationBuilder.CreateIndex(
                name: "IX_PlaidAccounts_PlaidItemId",
                table: "PlaidAccounts",
                column: "PlaidItemId");

            migrationBuilder.CreateIndex(
                name: "UQ_PlaidAccounts_PlaidAccountId",
                table: "PlaidAccounts",
                column: "PlaidAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaidItems_UserId",
                table: "PlaidItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_PlaidItems_PlaidItemId",
                table: "PlaidItems",
                column: "PlaidItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId_OccurredAt",
                table: "Transactions",
                columns: new[] { "AccountId", "OccurredAt" })
                .Annotation("Npgsql:IndexInclude", new[] { "TransactionType", "Amount", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId_OccurredAt",
                table: "Transactions",
                columns: new[] { "CategoryId", "OccurredAt" })
                .Annotation("Npgsql:IndexInclude", new[] { "TransactionType", "Amount", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransferAccountId",
                table: "Transactions",
                column: "TransferAccountId");

            migrationBuilder.CreateIndex(
                name: "UQ_Transactions_PlaidTransactionId",
                table: "Transactions",
                column: "PlaidTransactionId",
                unique: true,
                filter: "\"PlaidTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CognitoSub",
                table: "Users",
                column: "CognitoSub",
                unique: true,
                filter: "\"CognitoSub\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UQ_Categories_User_Name\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UQ_Accounts_User_Name\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UQ_BudgetPlans_User_Month_Name\";");

            migrationBuilder.DropTable(
                name: "BudgetPlanEntries");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "PlaidAccounts");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "BudgetPlans");

            migrationBuilder.DropTable(
                name: "PlaidItems");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
