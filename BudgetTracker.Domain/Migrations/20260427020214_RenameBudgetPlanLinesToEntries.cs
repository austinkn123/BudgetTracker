using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class RenameBudgetPlanLinesToEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BudgetPlanLines]')
                      AND [name] = N'FK_BudgetPlanLines_BudgetPlans_BudgetPlanId')
                BEGIN
                    ALTER TABLE [dbo].[BudgetPlanLines] DROP CONSTRAINT [FK_BudgetPlanLines_BudgetPlans_BudgetPlanId];
                END
                ELSE IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BudgetPlanLines]')
                      AND [name] = N'FK_BudgetPlanLines_BudgetPlans')
                BEGIN
                    ALTER TABLE [dbo].[BudgetPlanLines] DROP CONSTRAINT [FK_BudgetPlanLines_BudgetPlans];
                END
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BudgetPlanLines]')
                      AND [name] = N'FK_BudgetPlanLines_Categories_CategoryId')
                BEGIN
                    ALTER TABLE [dbo].[BudgetPlanLines] DROP CONSTRAINT [FK_BudgetPlanLines_Categories_CategoryId];
                END
                ELSE IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BudgetPlanLines]')
                      AND [name] = N'FK_BudgetPlanLines_Categories')
                BEGIN
                    ALTER TABLE [dbo].[BudgetPlanLines] DROP CONSTRAINT [FK_BudgetPlanLines_Categories];
                END
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetPlanLines",
                table: "BudgetPlanLines");

            migrationBuilder.RenameTable(
                name: "BudgetPlanLines",
                newName: "BudgetPlanEntries");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetPlanLines_CategoryId",
                table: "BudgetPlanEntries",
                newName: "IX_BudgetPlanEntries_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetPlanLines_Plan_Bucket_Type",
                table: "BudgetPlanEntries",
                newName: "IX_BudgetPlanEntries_Plan_Bucket_Type");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetPlanEntries",
                table: "BudgetPlanEntries",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetPlanEntries_BudgetPlans_BudgetPlanId",
                table: "BudgetPlanEntries",
                column: "BudgetPlanId",
                principalTable: "BudgetPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetPlanEntries_Categories_CategoryId",
                table: "BudgetPlanEntries",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");

            migrationBuilder.Sql("ALTER TABLE [dbo].[BudgetPlanEntries] DROP CONSTRAINT [CK_BudgetPlanLines_LineType];");
            migrationBuilder.Sql("ALTER TABLE [dbo].[BudgetPlanEntries] DROP CONSTRAINT [CK_BudgetPlanLines_Bucket];");
            migrationBuilder.Sql("ALTER TABLE [dbo].[BudgetPlanEntries] DROP CONSTRAINT [CK_BudgetPlanLines_Cadence];");
            migrationBuilder.Sql("ALTER TABLE [dbo].[BudgetPlanEntries] DROP CONSTRAINT [CK_BudgetPlanLines_Amount];");
            migrationBuilder.Sql("ALTER TABLE [dbo].[BudgetPlanEntries] DROP CONSTRAINT [CK_BudgetPlanLines_MonthlyEq];");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetPlanEntries_LineType",
                table: "BudgetPlanEntries",
                sql: "LineType IN ('Income', 'Expense')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetPlanEntries_Bucket",
                table: "BudgetPlanEntries",
                sql: "Bucket IN ('Core', 'Buffer')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetPlanEntries_Cadence",
                table: "BudgetPlanEntries",
                sql: "Cadence IN ('Monthly', 'Annual')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetPlanEntries_Amount",
                table: "BudgetPlanEntries",
                sql: "Amount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetPlanEntries_MonthlyEq",
                table: "BudgetPlanEntries",
                sql: "MonthlyEquivalent >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetPlanEntries_BudgetPlans_BudgetPlanId",
                table: "BudgetPlanEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetPlanEntries_Categories_CategoryId",
                table: "BudgetPlanEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BudgetPlanEntries",
                table: "BudgetPlanEntries");

            migrationBuilder.Sql("ALTER TABLE [dbo].[BudgetPlanEntries] DROP CONSTRAINT [CK_BudgetPlanEntries_LineType];");
            migrationBuilder.Sql("ALTER TABLE [dbo].[BudgetPlanEntries] DROP CONSTRAINT [CK_BudgetPlanEntries_Bucket];");
            migrationBuilder.Sql("ALTER TABLE [dbo].[BudgetPlanEntries] DROP CONSTRAINT [CK_BudgetPlanEntries_Cadence];");
            migrationBuilder.Sql("ALTER TABLE [dbo].[BudgetPlanEntries] DROP CONSTRAINT [CK_BudgetPlanEntries_Amount];");
            migrationBuilder.Sql("ALTER TABLE [dbo].[BudgetPlanEntries] DROP CONSTRAINT [CK_BudgetPlanEntries_MonthlyEq];");

            migrationBuilder.RenameTable(
                name: "BudgetPlanEntries",
                newName: "BudgetPlanLines");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetPlanEntries_CategoryId",
                table: "BudgetPlanLines",
                newName: "IX_BudgetPlanLines_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_BudgetPlanEntries_Plan_Bucket_Type",
                table: "BudgetPlanLines",
                newName: "IX_BudgetPlanLines_Plan_Bucket_Type");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BudgetPlanLines",
                table: "BudgetPlanLines",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetPlanLines_BudgetPlans",
                table: "BudgetPlanLines",
                column: "BudgetPlanId",
                principalTable: "BudgetPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetPlanLines_Categories",
                table: "BudgetPlanLines",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetPlanLines_LineType",
                table: "BudgetPlanLines",
                sql: "LineType IN ('Income', 'Expense')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetPlanLines_Bucket",
                table: "BudgetPlanLines",
                sql: "Bucket IN ('Core', 'Buffer')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetPlanLines_Cadence",
                table: "BudgetPlanLines",
                sql: "Cadence IN ('Monthly', 'Annual')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetPlanLines_Amount",
                table: "BudgetPlanLines",
                sql: "Amount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetPlanLines_MonthlyEq",
                table: "BudgetPlanLines",
                sql: "MonthlyEquivalent >= 0");
        }
    }
}
