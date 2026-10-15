using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetBoard.Database.Migrations
{
    /// <inheritdoc />
    public partial class RecurringRuleSuggestionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuggestionStatus",
                table: "RecurringRule",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Accepted");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringRule_UserID_SuggestionStatus",
                table: "RecurringRule",
                columns: new[] { "UserID", "SuggestionStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecurringRule_UserID_SuggestionStatus",
                table: "RecurringRule");

            migrationBuilder.DropColumn(
                name: "SuggestionStatus",
                table: "RecurringRule");
        }
    }
}
