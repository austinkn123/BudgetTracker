using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class RenameBudgetPlanEntriesPropertyAndDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.default_constraints
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BudgetPlanEntries]')
                      AND [name] = N'DF_BudgetPlanLines_IsStressFactor')
                BEGIN
                    EXEC sp_rename N'dbo.DF_BudgetPlanLines_IsStressFactor', N'DF_BudgetPlanEntries_IsStressFactor', N'OBJECT';
                END

                IF EXISTS (
                    SELECT 1
                    FROM sys.default_constraints
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BudgetPlanEntries]')
                      AND [name] = N'DF_BudgetPlanLines_SortOrder')
                BEGIN
                    EXEC sp_rename N'dbo.DF_BudgetPlanLines_SortOrder', N'DF_BudgetPlanEntries_SortOrder', N'OBJECT';
                END

                IF EXISTS (
                    SELECT 1
                    FROM sys.default_constraints
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BudgetPlanEntries]')
                      AND [name] = N'DF_BudgetPlanLines_CreatedAt')
                BEGIN
                    EXEC sp_rename N'dbo.DF_BudgetPlanLines_CreatedAt', N'DF_BudgetPlanEntries_CreatedAt', N'OBJECT';
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.default_constraints
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BudgetPlanEntries]')
                      AND [name] = N'DF_BudgetPlanEntries_IsStressFactor')
                BEGIN
                    EXEC sp_rename N'dbo.DF_BudgetPlanEntries_IsStressFactor', N'DF_BudgetPlanLines_IsStressFactor', N'OBJECT';
                END

                IF EXISTS (
                    SELECT 1
                    FROM sys.default_constraints
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BudgetPlanEntries]')
                      AND [name] = N'DF_BudgetPlanEntries_SortOrder')
                BEGIN
                    EXEC sp_rename N'dbo.DF_BudgetPlanEntries_SortOrder', N'DF_BudgetPlanLines_SortOrder', N'OBJECT';
                END

                IF EXISTS (
                    SELECT 1
                    FROM sys.default_constraints
                    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BudgetPlanEntries]')
                      AND [name] = N'DF_BudgetPlanEntries_CreatedAt')
                BEGIN
                    EXEC sp_rename N'dbo.DF_BudgetPlanEntries_CreatedAt', N'DF_BudgetPlanLines_CreatedAt', N'OBJECT';
                END
                """);
        }
    }
}
