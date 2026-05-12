using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyUniqueConstraints_AddNamedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the legacy auto-named UNIQUE CONSTRAINT that SQL Server created on the
            // Email column before EF migrations were introduced. The constraint name
            // UQ__Users__D3DDA2FFA4906BC1 is system-generated and cannot be referenced by
            // EF's fluent API — we must drop it via raw SQL before creating the
            // explicitly named replacement index below.
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM   sys.key_constraints
                    WHERE  name = 'UQ__Users__D3DDA2FFA4906BC1'
                    AND    parent_object_id = OBJECT_ID('dbo.Users')
                )
                BEGIN
                    ALTER TABLE dbo.Users DROP CONSTRAINT UQ__Users__D3DDA2FFA4906BC1;
                END
            ");

            migrationBuilder.CreateIndex(
                name: "UQ_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Users_Email",
                table: "Users");
        }
    }
}
