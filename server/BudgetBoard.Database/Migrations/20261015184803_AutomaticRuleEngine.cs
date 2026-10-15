using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetBoard.Database.Migrations
{
    /// <inheritdoc />
    public partial class AutomaticRuleEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MatchMode",
                table: "AutomaticRule",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "all");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "AutomaticRule",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "StopProcessing",
                table: "AutomaticRule",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatchMode",
                table: "AutomaticRule");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "AutomaticRule");

            migrationBuilder.DropColumn(
                name: "StopProcessing",
                table: "AutomaticRule");
        }
    }
}
