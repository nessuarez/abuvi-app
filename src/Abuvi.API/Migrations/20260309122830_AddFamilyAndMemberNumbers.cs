using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyAndMemberNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "member_number",
                table: "memberships",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "family_number",
                table: "family_units",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_memberships_member_number",
                table: "memberships",
                column: "member_number",
                unique: true,
                filter: "member_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_family_units_family_number",
                table: "family_units",
                column: "family_number",
                unique: true,
                filter: "family_number IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memberships_member_number",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "IX_family_units_family_number",
                table: "family_units");

            migrationBuilder.DropColumn(
                name: "member_number",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "family_number",
                table: "family_units");
        }
    }
}
