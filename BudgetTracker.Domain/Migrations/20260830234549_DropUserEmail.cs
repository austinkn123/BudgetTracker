using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class DropUserEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Users_Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            // Users.CognitoUserId is drift: it exists only in databases created before CognitoSub
            // superseded it, has never been mapped by the EF model or referenced by any code, and is
            // NULL for every row. EF cannot scaffold a drop for a column it does not know about, so
            // this is raw and guarded — a no-op on databases built from migrations.
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID('dbo.Users') AND [name] = 'IX_Users_CognitoUserId'
                )
                    DROP INDEX IX_Users_CognitoUserId ON dbo.Users;

                IF COL_LENGTH('dbo.Users', 'CognitoUserId') IS NOT NULL
                    ALTER TABLE dbo.Users DROP COLUMN CognitoUserId;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Users', 'CognitoUserId') IS NULL
                    ALTER TABLE dbo.Users ADD CognitoUserId NVARCHAR(255) NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID('dbo.Users') AND [name] = 'IX_Users_CognitoUserId'
                )
                    CREATE INDEX IX_Users_CognitoUserId ON dbo.Users (CognitoUserId);
            ");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "UQ_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }
    }
}
