using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHooks.Infrastructre.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryAttemptsAndResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "webhook_deliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "webhook_deliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastResponseSnippet",
                table: "webhook_deliveries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastStatusCode",
                table: "webhook_deliveries",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "webhook_deliveries");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "webhook_deliveries");

            migrationBuilder.DropColumn(
                name: "LastResponseSnippet",
                table: "webhook_deliveries");

            migrationBuilder.DropColumn(
                name: "LastStatusCode",
                table: "webhook_deliveries");
        }
    }
}
