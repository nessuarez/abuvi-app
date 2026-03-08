using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraUserInputFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "user_input",
                table: "registration_extras",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "requires_user_input",
                table: "camp_edition_extras",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "user_input_label",
                table: "camp_edition_extras",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "user_input",
                table: "registration_extras");

            migrationBuilder.DropColumn(
                name: "requires_user_input",
                table: "camp_edition_extras");

            migrationBuilder.DropColumn(
                name: "user_input_label",
                table: "camp_edition_extras");
        }
    }
}
