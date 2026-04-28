using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAccommodationZonesAndAssignmentProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "zone_id",
                table: "camp_edition_accommodations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "accommodation_assignment_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    camp_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accommodation_assignment_proposals", x => x.id);
                    table.ForeignKey(
                        name: "FK_accommodation_assignment_proposals_camp_editions_camp_editi~",
                        column: x => x.camp_edition_id,
                        principalTable: "camp_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "accommodation_zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    camp_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accommodation_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    max_capacity = table.Column<int>(type: "integer", nullable: true),
                    distribution_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accommodation_zones", x => x.id);
                    table.CheckConstraint("CK_AccommodationZones_MaxCapacity", "max_capacity IS NULL OR max_capacity > 0");
                    table.CheckConstraint("CK_AccommodationZones_SortOrder", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_accommodation_zones_camp_editions_camp_edition_id",
                        column: x => x.camp_edition_id,
                        principalTable: "camp_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "accommodation_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accommodation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accommodation_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_accommodation_assignments_accommodation_assignment_proposal~",
                        column: x => x.proposal_id,
                        principalTable: "accommodation_assignment_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_accommodation_assignments_camp_edition_accommodations_accom~",
                        column: x => x.accommodation_id,
                        principalTable: "camp_edition_accommodations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accommodation_assignments_registrations_registration_id",
                        column: x => x.registration_id,
                        principalTable: "registrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_camp_edition_accommodations_zone_id",
                table: "camp_edition_accommodations",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_accommodation_assignment_proposals_camp_edition_id",
                table: "accommodation_assignment_proposals",
                column: "camp_edition_id");

            migrationBuilder.CreateIndex(
                name: "IX_accommodation_assignments_accommodation_id",
                table: "accommodation_assignments",
                column: "accommodation_id");

            migrationBuilder.CreateIndex(
                name: "IX_accommodation_assignments_registration_id",
                table: "accommodation_assignments",
                column: "registration_id");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationAssignments_Proposal_Registration",
                table: "accommodation_assignments",
                columns: new[] { "proposal_id", "registration_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accommodation_zones_camp_edition_id",
                table: "accommodation_zones",
                column: "camp_edition_id");

            migrationBuilder.AddForeignKey(
                name: "FK_camp_edition_accommodations_accommodation_zones_zone_id",
                table: "camp_edition_accommodations",
                column: "zone_id",
                principalTable: "accommodation_zones",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_camp_edition_accommodations_accommodation_zones_zone_id",
                table: "camp_edition_accommodations");

            migrationBuilder.DropTable(
                name: "accommodation_assignments");

            migrationBuilder.DropTable(
                name: "accommodation_zones");

            migrationBuilder.DropTable(
                name: "accommodation_assignment_proposals");

            migrationBuilder.DropIndex(
                name: "IX_camp_edition_accommodations_zone_id",
                table: "camp_edition_accommodations");

            migrationBuilder.DropColumn(
                name: "zone_id",
                table: "camp_edition_accommodations");
        }
    }
}
