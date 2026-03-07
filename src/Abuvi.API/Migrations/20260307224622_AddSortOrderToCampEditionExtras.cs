using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSortOrderToCampEditionExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "camp_edition_extras",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CampEditionExtras_SortOrder",
                table: "camp_edition_extras",
                sql: "sort_order >= 0");

            // Set initial sort_order based on created_at ordering within each camp edition
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY camp_edition_id ORDER BY created_at) - 1 AS rn
                    FROM camp_edition_extras
                )
                UPDATE camp_edition_extras SET sort_order = ranked.rn
                FROM ranked WHERE camp_edition_extras.id = ranked.id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CampEditionExtras_SortOrder",
                table: "camp_edition_extras");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "camp_edition_extras");
        }
    }
}
