using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class FixGuestEntityModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: Guests table was already created by 20260217090219_AddGuestEntity.
            // This migration exists solely to repair the model snapshot, which was
            // missing the Guest entity definition due to a prior snapshot desync.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: matches empty Up().
        }
    }
}
