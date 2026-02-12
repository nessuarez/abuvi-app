using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialAdminUser_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed initial admin user for development/deployment bootstrap
            // Email: admin@abuvi.local
            // Password: Admin@123456 (BCrypt hashed with work factor 12)
            // IMPORTANT: Change password immediately on first login in production
            var adminId = new Guid("00000000-0000-0000-0000-000000000001");
            var now = DateTime.UtcNow;

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "email", "password_hash", "first_name", "last_name", "phone", "role", "is_active", "family_unit_id", "created_at", "updated_at" },
                values: new object[] {
                    adminId,
                    "admin@abuvi.local",
                    "$2a$12$WrQT1w74VIbQedAH4wbGkeQKEssHs.syVrNzW.cFX.nNZ3URJWjpC", // BCrypt hash of "Admin@123456"
                    "System",
                    "Administrator",
                    null,
                    "Admin",
                    true,
                    null,
                    now,
                    now
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seeded admin user when rolling back this migration
            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001")
            );
        }
    }
}
