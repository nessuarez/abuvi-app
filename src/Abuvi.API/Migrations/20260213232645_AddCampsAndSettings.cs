using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCampsAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "association_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    setting_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    setting_value = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_association_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "camps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    price_per_adult = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    price_per_child = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    price_per_baby = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camps", x => x.id);
                    table.CheckConstraint("CK_Camps_Latitude", "latitude IS NULL OR (latitude >= -90 AND latitude <= 90)");
                    table.CheckConstraint("CK_Camps_Longitude", "longitude IS NULL OR (longitude >= -180 AND longitude <= 180)");
                    table.CheckConstraint("CK_Camps_PricePerAdult", "price_per_adult >= 0");
                    table.CheckConstraint("CK_Camps_PricePerBaby", "price_per_baby >= 0");
                    table.CheckConstraint("CK_Camps_PricePerChild", "price_per_child >= 0");
                });

            migrationBuilder.CreateTable(
                name: "camp_editions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    price_per_adult = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    price_per_child = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    price_per_baby = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    use_custom_age_ranges = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    custom_baby_max_age = table.Column<int>(type: "integer", nullable: true),
                    custom_child_min_age = table.Column<int>(type: "integer", nullable: true),
                    custom_child_max_age = table.Column<int>(type: "integer", nullable: true),
                    custom_adult_min_age = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    max_capacity = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camp_editions", x => x.id);
                    table.CheckConstraint("CK_CampEditions_CustomAgeRanges", "NOT use_custom_age_ranges OR (custom_baby_max_age IS NOT NULL AND custom_child_min_age IS NOT NULL AND custom_child_max_age IS NOT NULL AND custom_adult_min_age IS NOT NULL)");
                    table.CheckConstraint("CK_CampEditions_PricePerAdult", "price_per_adult >= 0");
                    table.CheckConstraint("CK_CampEditions_PricePerBaby", "price_per_baby >= 0");
                    table.CheckConstraint("CK_CampEditions_PricePerChild", "price_per_child >= 0");
                    table.ForeignKey(
                        name: "FK_camp_editions_camps_camp_id",
                        column: x => x.camp_id,
                        principalTable: "camps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "camp_edition_extras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    pricing_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pricing_period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    max_quantity = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camp_edition_extras", x => x.id);
                    table.CheckConstraint("CK_CampEditionExtras_MaxQuantity", "max_quantity IS NULL OR max_quantity > 0");
                    table.CheckConstraint("CK_CampEditionExtras_Price", "price >= 0");
                    table.ForeignKey(
                        name: "FK_camp_edition_extras_camp_editions_camp_edition_id",
                        column: x => x.camp_edition_id,
                        principalTable: "camp_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssociationSettings_SettingKey",
                table: "association_settings",
                column: "setting_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_camp_edition_extras_camp_edition_id",
                table: "camp_edition_extras",
                column: "camp_edition_id");

            migrationBuilder.CreateIndex(
                name: "IX_CampEditions_CampId_Year",
                table: "camp_editions",
                columns: new[] { "camp_id", "year" });

            // Seed default age ranges
            migrationBuilder.InsertData(
                table: "association_settings",
                columns: new[] { "id", "setting_key", "setting_value", "updated_at" },
                values: new object[] {
                    Guid.NewGuid(),
                    "age_ranges",
                    "{\"babyMaxAge\":2,\"childMinAge\":3,\"childMaxAge\":12,\"adultMinAge\":13}",
                    DateTime.UtcNow
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seed data
            migrationBuilder.DeleteData(
                table: "association_settings",
                keyColumn: "setting_key",
                keyValue: "age_ranges");

            migrationBuilder.DropTable(
                name: "association_settings");

            migrationBuilder.DropTable(
                name: "camp_edition_extras");

            migrationBuilder.DropTable(
                name: "camp_editions");

            migrationBuilder.DropTable(
                name: "camps");
        }
    }
}
