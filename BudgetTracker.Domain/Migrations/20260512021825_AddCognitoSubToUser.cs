using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitoSubToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CognitoSub",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CognitoSub",
                table: "Users",
                column: "CognitoSub",
                unique: true,
                filter: "[CognitoSub] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CognitoSub",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CognitoSub",
                table: "Users");
        }
    }
}
