using Abuvi.API.Features.Users;
using Abuvi.API.Features.FamilyUnits;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Data;

public class AbuviDbContext(DbContextOptions<AbuviDbContext> options) : DbContext(options)
{
    // Entity DbSets
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRoleChangeLog> UserRoleChangeLogs => Set<UserRoleChangeLog>();
    public DbSet<FamilyUnit> FamilyUnits => Set<FamilyUnit>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbuviDbContext).Assembly);
    }
}
