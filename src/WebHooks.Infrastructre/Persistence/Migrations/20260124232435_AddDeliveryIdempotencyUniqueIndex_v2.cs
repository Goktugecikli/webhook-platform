using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHooks.Infrastructre.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryIdempotencyUniqueIndex_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_webhook_deliveries_Provider_IdempotencyKey",
                table: "webhook_deliveries");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "webhook_deliveries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TargetUrl",
                table: "webhook_deliveries",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "webhook_deliveries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                table: "webhook_deliveries",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                table: "webhook_deliveries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "webhook_deliveries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_Status_NextRetryAt",
                table: "webhook_deliveries",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_TenantId_Provider_IdempotencyKey_TargetU~",
                table: "webhook_deliveries",
                columns: new[] { "TenantId", "Provider", "IdempotencyKey", "TargetUrl" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_webhook_deliveries_Status_NextRetryAt",
                table: "webhook_deliveries");

            migrationBuilder.DropIndex(
                name: "IX_webhook_deliveries_TenantId_Provider_IdempotencyKey_TargetU~",
                table: "webhook_deliveries");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "webhook_deliveries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "TargetUrl",
                table: "webhook_deliveries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "webhook_deliveries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                table: "webhook_deliveries",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                table: "webhook_deliveries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "webhook_deliveries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_Provider_IdempotencyKey",
                table: "webhook_deliveries",
                columns: new[] { "Provider", "IdempotencyKey" },
                unique: true);
        }
    }
}
