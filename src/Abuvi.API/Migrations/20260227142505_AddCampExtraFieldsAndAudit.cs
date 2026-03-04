using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCampExtraFieldsAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "abuvi_contacted_at",
                table: "camps",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "abuvi_has_data_errors",
                table: "camps",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "abuvi_last_visited",
                table: "camps",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "abuvi_managed_by_user_id",
                table: "camps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "abuvi_possibility",
                table: "camps",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "base_price",
                table: "camps",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_company",
                table: "camps",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "camps",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_person",
                table: "camps",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "external_source_id",
                table: "camps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_modified_by_user_id",
                table: "camps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "province",
                table: "camps",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "secondary_website_url",
                table: "camps",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "vat_included",
                table: "camps",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "camp_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camp_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_camp_audit_logs_camps_camp_id",
                        column: x => x.camp_id,
                        principalTable: "camps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "camp_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    season = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camp_observations", x => x.id);
                    table.ForeignKey(
                        name: "FK_camp_observations_camps_camp_id",
                        column: x => x.camp_id,
                        principalTable: "camps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_camps_abuvi_managed_by_user_id",
                table: "camps",
                column: "abuvi_managed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_camps_external_source_id",
                table: "camps",
                column: "external_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_camp_audit_logs_camp_id",
                table: "camp_audit_logs",
                column: "camp_id");

            migrationBuilder.CreateIndex(
                name: "ix_camp_audit_logs_camp_id_changed_at",
                table: "camp_audit_logs",
                columns: new[] { "camp_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_camp_observations_camp_id",
                table: "camp_observations",
                column: "camp_id");

            migrationBuilder.AddForeignKey(
                name: "FK_camps_users_abuvi_managed_by_user_id",
                table: "camps",
                column: "abuvi_managed_by_user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_camps_users_abuvi_managed_by_user_id",
                table: "camps");

            migrationBuilder.DropTable(
                name: "camp_audit_logs");

            migrationBuilder.DropTable(
                name: "camp_observations");

            migrationBuilder.DropIndex(
                name: "IX_camps_abuvi_managed_by_user_id",
                table: "camps");

            migrationBuilder.DropIndex(
                name: "ix_camps_external_source_id",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "abuvi_contacted_at",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "abuvi_has_data_errors",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "abuvi_last_visited",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "abuvi_managed_by_user_id",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "abuvi_possibility",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "base_price",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "contact_company",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "contact_person",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "external_source_id",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "last_modified_by_user_id",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "province",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "secondary_website_url",
                table: "camps");

            migrationBuilder.DropColumn(
                name: "vat_included",
                table: "camps");
        }
    }
}
