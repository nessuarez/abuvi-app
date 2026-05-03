using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "draft_target_status",
                table: "registrations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_pending_user_acknowledgement",
                table: "registrations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "registration_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    new_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_registration_status_history_registrations_registration_id",
                        column: x => x.registration_id,
                        principalTable: "registrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_registration_status_history_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_registration_status_history_changed_by_user_id",
                table: "registration_status_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationStatusHistory_RegistrationId",
                table: "registration_status_history",
                column: "registration_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registration_status_history");

            migrationBuilder.DropColumn(
                name: "draft_target_status",
                table: "registrations");

            migrationBuilder.DropColumn(
                name: "has_pending_user_acknowledgement",
                table: "registrations");
        }
    }
}
