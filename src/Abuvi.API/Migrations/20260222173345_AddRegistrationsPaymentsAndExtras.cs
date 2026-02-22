using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationsPaymentsAndExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    family_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_edition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_total_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    extras_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    total_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrations", x => x.id);
                    table.CheckConstraint("CK_Registrations_TotalAmount", "total_amount = base_total_amount + extras_amount");
                    table.ForeignKey(
                        name: "FK_registrations_camp_editions_camp_edition_id",
                        column: x => x.camp_edition_id,
                        principalTable: "camp_editions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrations_family_units_family_unit_id",
                        column: x => x.family_unit_id,
                        principalTable: "family_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrations_users_registered_by_user_id",
                        column: x => x.registered_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    external_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.CheckConstraint("CK_Payments_Amount", "amount > 0");
                    table.ForeignKey(
                        name: "FK_payments_registrations_registration_id",
                        column: x => x.registration_id,
                        principalTable: "registrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registration_extras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_edition_extra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    camp_duration_days = table.Column<int>(type: "integer", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_extras", x => x.id);
                    table.ForeignKey(
                        name: "FK_registration_extras_camp_edition_extras_camp_edition_extra_~",
                        column: x => x.camp_edition_extra_id,
                        principalTable: "camp_edition_extras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_extras_registrations_registration_id",
                        column: x => x.registration_id,
                        principalTable: "registrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registration_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    age_at_camp = table.Column<int>(type: "integer", nullable: false),
                    age_category = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    individual_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_registration_members_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registration_members_registrations_registration_id",
                        column: x => x.registration_id,
                        principalTable: "registrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RegistrationId",
                table: "payments",
                column: "registration_id");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                table: "payments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_registration_extras_camp_edition_extra_id",
                table: "registration_extras",
                column: "camp_edition_extra_id");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationExtras_RegistrationId_CampEditionExtraId",
                table: "registration_extras",
                columns: new[] { "registration_id", "camp_edition_extra_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registration_members_family_member_id",
                table: "registration_members",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationMembers_RegistrationId",
                table: "registration_members",
                column: "registration_id");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationMembers_RegistrationId_FamilyMemberId",
                table: "registration_members",
                columns: new[] { "registration_id", "family_member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_CampEditionId",
                table: "registrations",
                column: "camp_edition_id");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_FamilyUnitId_CampEditionId",
                table: "registrations",
                columns: new[] { "family_unit_id", "camp_edition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registrations_registered_by_user_id",
                table: "registrations",
                column: "registered_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_Status",
                table: "registrations",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "registration_extras");

            migrationBuilder.DropTable(
                name: "registration_members");

            migrationBuilder.DropTable(
                name: "registrations");
        }
    }
}
