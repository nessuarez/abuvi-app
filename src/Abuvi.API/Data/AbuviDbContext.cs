using Abuvi.API.Features.Users;
using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbuviDbContext).Assembly);
    }
}
