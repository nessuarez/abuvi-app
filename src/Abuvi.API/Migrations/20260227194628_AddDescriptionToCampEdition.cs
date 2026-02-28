using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDescriptionToCampEdition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "camp_editions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                table: "camp_editions");
        }
    }
}
