using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantityToAccommodations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "quantity",
                table: "camp_edition_accommodations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "unit_index",
                table: "accommodation_assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CampEditionAccommodations_Quantity",
                table: "camp_edition_accommodations",
                sql: "quantity > 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationAssignments_Proposal_Accommodation_UnitIndex",
                table: "accommodation_assignments",
                columns: new[] { "proposal_id", "accommodation_id", "unit_index" },
                unique: true,
                filter: "unit_index IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CampEditionAccommodations_Quantity",
                table: "camp_edition_accommodations");

            migrationBuilder.DropIndex(
                name: "IX_AccommodationAssignments_Proposal_Accommodation_UnitIndex",
                table: "accommodation_assignments");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "camp_edition_accommodations");

            migrationBuilder.DropColumn(
                name: "unit_index",
                table: "accommodation_assignments");
        }
    }
}
