using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAccommodationMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_media_items_accommodation_id",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "IX_media_items_zone_id",
                table: "media_items");

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "media_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_primary",
                table: "media_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "accommodation_type_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    accommodation_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    thumbnail_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accommodation_type_media", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_accommodation_primary",
                table: "media_items",
                columns: new[] { "accommodation_id", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_zone_primary",
                table: "media_items",
                columns: new[] { "zone_id", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "ix_accommodation_type_media_type",
                table: "accommodation_type_media",
                column: "accommodation_type");

            migrationBuilder.CreateIndex(
                name: "ix_accommodation_type_media_type_primary",
                table: "accommodation_type_media",
                columns: new[] { "accommodation_type", "is_primary" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accommodation_type_media");

            migrationBuilder.DropIndex(
                name: "ix_media_items_accommodation_primary",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "ix_media_items_zone_primary",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "is_primary",
                table: "media_items");

            migrationBuilder.CreateIndex(
                name: "IX_media_items_accommodation_id",
                table: "media_items",
                column: "accommodation_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_items_zone_id",
                table: "media_items",
                column: "zone_id");
        }
    }
}
