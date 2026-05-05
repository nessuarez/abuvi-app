using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abuvi.API.Migrations
{
    /// <inheritdoc />
    public partial class BackfillRegistrationStatusFromPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Transition Pending registrations with a confirmed payment to PartiallyPaid,
            // then insert a synthetic history entry so the timeline reflects the change.
            migrationBuilder.Sql("""
                WITH affected AS (
                    UPDATE registrations r
                    SET status = 'PartiallyPaid', updated_at = NOW()
                    FROM (
                        SELECT DISTINCT registration_id
                        FROM payments
                        WHERE status = 'Completed'
                    ) p
                    WHERE r.id = p.registration_id
                      AND r.status = 'Pending'
                    RETURNING r.id
                )
                INSERT INTO registration_status_history
                    (id, registration_id, previous_status, new_status,
                     changed_by_user_id, changed_at, trigger, notes)
                SELECT
                    gen_random_uuid(), id, 'Pending', 'PartiallyPaid',
                    NULL, NOW(), 'Automatic', 'Plazo 1 confirmado (migración)'
                FROM affected;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM registration_status_history
                WHERE notes = 'Plazo 1 confirmado (migración)';

                UPDATE registrations SET status = 'Pending', updated_at = NOW()
                WHERE status = 'PartiallyPaid';
                """);
        }
    }
}
