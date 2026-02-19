using Abuvi.API.Features.Users;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Guests;
using Abuvi.API.Features.Memberships;
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
    public DbSet<AssociationSettings> AssociationSettings => Set<AssociationSettings>();
    public DbSet<CampPhoto> CampPhotos => Set<CampPhoto>();
    public DbSet<FamilyUnit> FamilyUnits => Set<FamilyUnit>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<MembershipFee> MembershipFees => Set<MembershipFee>();
    public DbSet<Guest> Guests => Set<Guest>();

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
