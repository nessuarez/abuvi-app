using Abuvi.API.Features.Users;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Guests;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Memories;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Abuvi.API.Data;

public class AbuviDbContext(DbContextOptions<AbuviDbContext> options) : DbContext(options)
{
    // Entity DbSets
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRoleChangeLog> UserRoleChangeLogs => Set<UserRoleChangeLog>();
    public DbSet<Camp> Camps => Set<Camp>();
    public DbSet<CampEdition> CampEditions => Set<CampEdition>();
    public DbSet<CampEditionExtra> CampEditionExtras => Set<CampEditionExtra>();
    public DbSet<CampEditionAccommodation> CampEditionAccommodations => Set<CampEditionAccommodation>();
    public DbSet<AssociationSettings> AssociationSettings => Set<AssociationSettings>();
    public DbSet<CampPhoto> CampPhotos => Set<CampPhoto>();
    public DbSet<CampObservation> CampObservations => Set<CampObservation>();
    public DbSet<CampAuditLog> CampAuditLogs => Set<CampAuditLog>();
    public DbSet<FamilyUnit> FamilyUnits => Set<FamilyUnit>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<MembershipFee> MembershipFees => Set<MembershipFee>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<RegistrationMember> RegistrationMembers => Set<RegistrationMember>();
    public DbSet<RegistrationExtra> RegistrationExtras => Set<RegistrationExtra>();
    public DbSet<RegistrationAccommodationPreference> RegistrationAccommodationPreferences => Set<RegistrationAccommodationPreference>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Memory> Memories => Set<Memory>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // PostgreSQL timestamptz requires UTC; treat all unspecified DateTimes as UTC globally.
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbuviDbContext).Assembly);
    }

    private sealed class UtcDateTimeConverter()
        : ValueConverter<DateTime, DateTime>(
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
