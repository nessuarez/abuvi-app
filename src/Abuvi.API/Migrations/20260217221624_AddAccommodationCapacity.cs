using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAccommodationCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "accommodation_capacity_json",
                table: "camps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "camp_photos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "accommodation_capacity_json",
                table: "camp_editions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accommodation_capacity_json",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "description",
                table: "camp_photos");

            migrationBuilder.DropColumn(
                name: "accommodation_capacity_json",
                table: "camp_editions");
        }
    }
}
