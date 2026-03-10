using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddHasPetToRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_pet",
                table: "registrations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_pet",
                table: "registrations");
        }
    }
}
