using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetBoard.Database.Migrations
{
    /// <inheritdoc />
    public partial class EnvelopeBudgeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnvelopeAccount",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvelopeAccount", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EnvelopeAccount_Account_AccountID",
                        column: x => x.AccountID,
                        principalTable: "Account",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnvelopeAccount_User_UserID",
                        column: x => x.UserID,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvelopeAssignment",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Assigned = table.Column<decimal>(type: "numeric", nullable: false),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvelopeAssignment", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EnvelopeAssignment_User_UserID",
                        column: x => x.UserID,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvelopeSettings",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvelopeSettings", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EnvelopeSettings_User_UserID",
                        column: x => x.UserID,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnvelopeAccount_AccountID",
                table: "EnvelopeAccount",
                column: "AccountID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnvelopeAccount_UserID",
                table: "EnvelopeAccount",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_EnvelopeAssignment_UserID_Category_Month",
                table: "EnvelopeAssignment",
                columns: new[] { "UserID", "Category", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnvelopeSettings_UserID",
                table: "EnvelopeSettings",
                column: "UserID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnvelopeAccount");

            migrationBuilder.DropTable(
                name: "EnvelopeAssignment");

            migrationBuilder.DropTable(
                name: "EnvelopeSettings");
        }
    }
}
