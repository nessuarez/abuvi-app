using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInstallmentAndProofFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "admin_notes",
                table: "payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "confirmed_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "confirmed_by_user_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "due_date",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "installment_number",
                table: "payments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "proof_file_name",
                table: "payments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proof_file_url",
                table: "payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "proof_uploaded_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transfer_concept",
                table: "payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TransferConcept",
                table: "payments",
                column: "transfer_concept");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_TransferConcept",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "admin_notes",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "confirmed_by_user_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "due_date",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "installment_number",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "proof_file_name",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "proof_file_url",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "proof_uploaded_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "transfer_concept",
                table: "payments");
        }
    }
}
