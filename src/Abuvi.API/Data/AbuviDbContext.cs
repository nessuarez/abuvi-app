using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Data;

public class AbuviDbContext(DbContextOptions<AbuviDbContext> options) : DbContext(options)
{
    // DbSets will be added as entities are created
    // Example: public DbSet<Camp> Camps => Set<Camp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbuviDbContext).Assembly);
    }
}
