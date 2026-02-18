using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedCampInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "administrative_area",
                table: "camps",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "business_status",
                table: "camps",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "camps",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "formatted_address",
                table: "camps",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "google_maps_url",
                table: "camps",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "google_rating",
                table: "camps",
                type: "numeric(3,1)",
                precision: 3,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "google_rating_count",
                table: "camps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_google_sync_at",
                table: "camps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "locality",
                table: "camps",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "national_phone_number",
                table: "camps",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "camps",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "place_types",
                table: "camps",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "postal_code",
                table: "camps",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "street_address",
                table: "camps",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website_url",
                table: "camps",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "camp_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    photo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    attribution_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    attribution_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_original = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camp_photos", x => x.id);
                    table.ForeignKey(
                        name: "FK_camp_photos_camps_camp_id",
                        column: x => x.camp_id,
                        principalTable: "camps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_camp_photos_camp_id",
                table: "camp_photos",
                column: "camp_id");

            migrationBuilder.CreateIndex(
                name: "ix_camp_photos_camp_id_is_primary",
                table: "camp_photos",
                columns: new[] { "camp_id", "is_primary" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "camp_photos");

            migrationBuilder.DropColumn(
                name: "administrative_area",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "business_status",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "country",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "formatted_address",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "google_maps_url",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "google_rating",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "google_rating_count",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "last_google_sync_at",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "locality",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "national_phone_number",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "place_types",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "postal_code",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "street_address",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "website_url",
                table: "camps");
        }
    }
}
