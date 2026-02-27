using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationGuardianAndPreferenceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "campates_preference",
                table: "registrations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "special_needs",
                table: "registrations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "guardian_document_number",
                table: "registration_members",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "guardian_name",
                table: "registration_members",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "campates_preference",
                table: "registrations");

            migrationBuilder.DropColumn(
                name: "special_needs",
                table: "registrations");

            migrationBuilder.DropColumn(
                name: "guardian_document_number",
                table: "registration_members");

            migrationBuilder.DropColumn(
                name: "guardian_name",
                table: "registration_members");
        }
    }
}
