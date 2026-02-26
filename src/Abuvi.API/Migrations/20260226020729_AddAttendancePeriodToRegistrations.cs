using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendancePeriodToRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attendance_period",
                table: "registration_members",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "Complete");

            migrationBuilder.AddColumn<DateOnly>(
                name: "visit_end_date",
                table: "registration_members",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "visit_start_date",
                table: "registration_members",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "half_date",
                table: "camp_editions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_weekend_capacity",
                table: "camp_editions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_adult_week",
                table: "camp_editions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_adult_weekend",
                table: "camp_editions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_baby_week",
                table: "camp_editions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_baby_weekend",
                table: "camp_editions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_child_week",
                table: "camp_editions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "price_per_child_weekend",
                table: "camp_editions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "weekend_end_date",
                table: "camp_editions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "weekend_start_date",
                table: "camp_editions",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attendance_period",
                table: "registration_members");

            migrationBuilder.DropColumn(
                name: "visit_end_date",
                table: "registration_members");

            migrationBuilder.DropColumn(
                name: "visit_start_date",
                table: "registration_members");

            migrationBuilder.DropColumn(
                name: "half_date",
                table: "camp_editions");

            migrationBuilder.DropColumn(
                name: "max_weekend_capacity",
                table: "camp_editions");

            migrationBuilder.DropColumn(
                name: "price_per_adult_week",
                table: "camp_editions");

            migrationBuilder.DropColumn(
                name: "price_per_adult_weekend",
                table: "camp_editions");

            migrationBuilder.DropColumn(
                name: "price_per_baby_week",
                table: "camp_editions");

            migrationBuilder.DropColumn(
                name: "price_per_baby_weekend",
                table: "camp_editions");

            migrationBuilder.DropColumn(
                name: "price_per_child_week",
                table: "camp_editions");

            migrationBuilder.DropColumn(
                name: "price_per_child_weekend",
                table: "camp_editions");

            migrationBuilder.DropColumn(
                name: "weekend_end_date",
                table: "camp_editions");

            migrationBuilder.DropColumn(
                name: "weekend_start_date",
                table: "camp_editions");
        }
    }
}
