using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCampEditionAccommodations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "camp_edition_accommodations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    accommodation_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camp_edition_accommodations", x => x.id);
                    table.CheckConstraint("CK_CampEditionAccommodations_Capacity", "capacity IS NULL OR capacity > 0");
                    table.CheckConstraint("CK_CampEditionAccommodations_SortOrder", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_camp_edition_accommodations_camp_editions_camp_edition_id",
                        column: x => x.camp_edition_id,
                        principalTable: "camp_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registration_accommodation_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_edition_accommodation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preference_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_accommodation_preferences", x => x.id);
                    table.CheckConstraint("CK_RegAccommPrefs_PreferenceOrder", "preference_order >= 1 AND preference_order <= 3");
                    table.ForeignKey(
                        name: "FK_registration_accommodation_preferences_camp_edition_accommo~",
                        column: x => x.camp_edition_accommodation_id,
                        principalTable: "camp_edition_accommodations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_accommodation_preferences_registrations_regist~",
                        column: x => x.registration_id,
                        principalTable: "registrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_camp_edition_accommodations_camp_edition_id",
                table: "camp_edition_accommodations",
                column: "camp_edition_id");

            migrationBuilder.CreateIndex(
                name: "IX_RegAccommPrefs_RegistrationId_AccommodationId",
                table: "registration_accommodation_preferences",
                columns: new[] { "registration_id", "camp_edition_accommodation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegAccommPrefs_RegistrationId_PreferenceOrder",
                table: "registration_accommodation_preferences",
                columns: new[] { "registration_id", "preference_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registration_accommodation_preferences_camp_edition_accommo~",
                table: "registration_accommodation_preferences",
                column: "camp_edition_accommodation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registration_accommodation_preferences");

            migrationBuilder.DropTable(
                name: "camp_edition_accommodations");
        }
    }
}
