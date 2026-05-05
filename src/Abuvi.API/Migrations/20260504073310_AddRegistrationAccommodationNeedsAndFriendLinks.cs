using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationAccommodationNeedsAndFriendLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "accommodation_internal_notes",
                table: "registrations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "registration_accommodation_needs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accommodation_feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tagged_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_accommodation_needs", x => x.id);
                    table.ForeignKey(
                        name: "FK_registration_accommodation_needs_accommodation_features_acc~",
                        column: x => x.accommodation_feature_id,
                        principalTable: "accommodation_features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_accommodation_needs_registrations_registration~",
                        column: x => x.registration_id,
                        principalTable: "registrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registration_friend_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_friend_links", x => x.id);
                    table.CheckConstraint("CK_RegistrationFriendLinks_NoSelfLink", "registration_id <> linked_registration_id");
                    table.ForeignKey(
                        name: "FK_registration_friend_links_registrations_linked_registration~",
                        column: x => x.linked_registration_id,
                        principalTable: "registrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_registration_friend_links_registrations_registration_id",
                        column: x => x.registration_id,
                        principalTable: "registrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_registration_accommodation_needs_accommodation_feature_id",
                table: "registration_accommodation_needs",
                column: "accommodation_feature_id");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationAccommodationNeeds_RegistrationId",
                table: "registration_accommodation_needs",
                column: "registration_id");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationAccommodationNeeds_RegistrationId_FeatureId",
                table: "registration_accommodation_needs",
                columns: new[] { "registration_id", "accommodation_feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registration_friend_links_linked_registration_id",
                table: "registration_friend_links",
                column: "linked_registration_id");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationFriendLinks_RegistrationId",
                table: "registration_friend_links",
                column: "registration_id");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationFriendLinks_RegistrationId_LinkedId",
                table: "registration_friend_links",
                columns: new[] { "registration_id", "linked_registration_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registration_accommodation_needs");

            migrationBuilder.DropTable(
                name: "registration_friend_links");

            migrationBuilder.DropColumn(
                name: "accommodation_internal_notes",
                table: "registrations");
        }
    }
}
