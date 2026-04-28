using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAccommodationFeaturesAndZoneAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "accommodation_id",
                table: "media_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "zone_id",
                table: "media_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "accommodation_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    applicability_level = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accommodation_features", x => x.id);
                    table.CheckConstraint("CK_AccommodationFeatures_SortOrder", "sort_order >= 0");
                });

            migrationBuilder.CreateTable(
                name: "accommodation_feature_assignments",
                columns: table => new
                {
                    accommodation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accommodation_feature_assignments", x => new { x.accommodation_id, x.feature_id });
                    table.ForeignKey(
                        name: "FK_accommodation_feature_assignments_accommodation_features_fe~",
                        column: x => x.feature_id,
                        principalTable: "accommodation_features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accommodation_feature_assignments_camp_edition_accommodatio~",
                        column: x => x.accommodation_id,
                        principalTable: "camp_edition_accommodations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zone_feature_assignments",
                columns: table => new
                {
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zone_feature_assignments", x => new { x.zone_id, x.feature_id });
                    table.ForeignKey(
                        name: "FK_zone_feature_assignments_accommodation_features_feature_id",
                        column: x => x.feature_id,
                        principalTable: "accommodation_features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_zone_feature_assignments_accommodation_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "accommodation_zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_media_items_accommodation_id",
                table: "media_items",
                column: "accommodation_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_items_zone_id",
                table: "media_items",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_accommodation_feature_assignments_feature_id",
                table: "accommodation_feature_assignments",
                column: "feature_id");

            migrationBuilder.CreateIndex(
                name: "IX_accommodation_features_name",
                table: "accommodation_features",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zone_feature_assignments_feature_id",
                table: "zone_feature_assignments",
                column: "feature_id");

            migrationBuilder.AddForeignKey(
                name: "FK_media_items_accommodation_zones_zone_id",
                table: "media_items",
                column: "zone_id",
                principalTable: "accommodation_zones",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_media_items_camp_edition_accommodations_accommodation_id",
                table: "media_items",
                column: "accommodation_id",
                principalTable: "camp_edition_accommodations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_media_items_accommodation_zones_zone_id",
                table: "media_items");

            migrationBuilder.DropForeignKey(
                name: "FK_media_items_camp_edition_accommodations_accommodation_id",
                table: "media_items");

            migrationBuilder.DropTable(
                name: "accommodation_feature_assignments");

            migrationBuilder.DropTable(
                name: "zone_feature_assignments");

            migrationBuilder.DropTable(
                name: "accommodation_features");

            migrationBuilder.DropIndex(
                name: "IX_media_items_accommodation_id",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "IX_media_items_zone_id",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "accommodation_id",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "zone_id",
                table: "media_items");
        }
    }
}
