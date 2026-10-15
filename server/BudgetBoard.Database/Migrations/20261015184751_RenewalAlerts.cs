using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetBoard.Database.Migrations
{
    /// <inheritdoc />
    public partial class RenewalAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RenewalAlertDelivery",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurringRuleID = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalAlertDelivery", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RenewalAlertDelivery_RecurringRule_RecurringRuleID",
                        column: x => x.RecurringRuleID,
                        principalTable: "RecurringRule",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RenewalAlertSettings",
                columns: table => new
                {
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DaysBefore = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    DiscordEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DiscordWebhookUrl = table.Column<string>(type: "text", nullable: true),
                    TelegramEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TelegramBotToken = table.Column<string>(type: "text", nullable: true),
                    TelegramChatId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WebhookEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WebhookUrl = table.Column<string>(type: "text", nullable: true),
                    WebhookToken = table.Column<string>(type: "text", nullable: true),
                    WebhookFormat = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "json")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalAlertSettings", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_RenewalAlertSettings_User_UserID",
                        column: x => x.UserID,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionRenewalPreference",
                columns: table => new
                {
                    RecurringRuleID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DaysBefore = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionRenewalPreference", x => x.RecurringRuleID);
                    table.ForeignKey(
                        name: "FK_SubscriptionRenewalPreference_RecurringRule_RecurringRuleID",
                        column: x => x.RecurringRuleID,
                        principalTable: "RecurringRule",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalAlertDelivery_RecurringRuleID_OccurrenceDate_Channel",
                table: "RenewalAlertDelivery",
                columns: new[] { "RecurringRuleID", "OccurrenceDate", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenewalAlertDelivery_UserID",
                table: "RenewalAlertDelivery",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRenewalPreference_UserID",
                table: "SubscriptionRenewalPreference",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RenewalAlertDelivery");

            migrationBuilder.DropTable(
                name: "RenewalAlertSettings");

            migrationBuilder.DropTable(
                name: "SubscriptionRenewalPreference");
        }
    }
}
