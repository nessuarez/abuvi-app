using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(timestamp DESC);
                CREATE INDEX IF NOT EXISTS idx_logs_level ON logs(level);
                CREATE INDEX IF NOT EXISTS idx_logs_user_id ON logs(user_id) WHERE user_id IS NOT NULL;
                CREATE INDEX IF NOT EXISTS idx_logs_correlation_id ON logs(correlation_id) WHERE correlation_id IS NOT NULL;
                CREATE INDEX IF NOT EXISTS idx_logs_properties ON logs USING gin(properties);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS idx_logs_timestamp;
                DROP INDEX IF EXISTS idx_logs_level;
                DROP INDEX IF EXISTS idx_logs_user_id;
                DROP INDEX IF EXISTS idx_logs_correlation_id;
                DROP INDEX IF EXISTS idx_logs_properties;
            ");
        }
    }
}
