using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amount",
                table: "payments");

            migrationBuilder.AddColumn<bool>(
                name: "concept_overridden",
                table: "payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "original_amount",
                table: "payments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concept_overridden",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "original_amount",
                table: "payments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amount",
                table: "payments",
                sql: "amount > 0");
        }
    }
}
