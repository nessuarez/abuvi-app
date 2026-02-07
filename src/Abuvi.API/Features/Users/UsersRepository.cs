using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Repository implementation for User data access using EF Core
/// </summary>
public class UsersRepository(AbuviDbContext context) : IUsersRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<List<User>> GetAllAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.Id = Guid.NewGuid();
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.UpdatedAt = DateTime.UtcNow;

        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FindAsync([id], cancellationToken);
        if (user is null)
            return false;

        context.Users.Remove(user);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AnyAsync(u => u.Email == email, cancellationToken);
    }
}
