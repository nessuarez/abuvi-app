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

    public async Task<User?> GetByVerificationTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == token, cancellationToken);
    }

    public async Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default)
        => await context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token, cancellationToken);

    public async Task<(List<User> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, UserRole? role, bool? isActive,
        string? sortBy, string? sortOrder, CancellationToken cancellationToken = default)
    {
        IQueryable<User> query = context.Users.AsNoTracking();

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term) ||
                (u.FirstName + " " + u.LastName).ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ApplySort(query, sortBy, sortOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Applies an allowlisted sort (unknown values fall back to createdAt desc) with Id as a stable tiebreaker
    /// </summary>
    private static IOrderedQueryable<User> ApplySort(IQueryable<User> query, string? sortBy, string? sortOrder)
    {
        // A recognised field without an explicit "desc" sorts ascending
        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<User>? ordered = (sortBy?.ToLowerInvariant(), descending) switch
        {
            ("email", false) => query.OrderBy(u => u.Email),
            ("email", true) => query.OrderByDescending(u => u.Email),
            ("firstname", false) => query.OrderBy(u => u.FirstName),
            ("firstname", true) => query.OrderByDescending(u => u.FirstName),
            ("lastname", false) => query.OrderBy(u => u.LastName),
            ("lastname", true) => query.OrderByDescending(u => u.LastName),
            ("role", false) => query.OrderBy(u => u.Role),
            ("role", true) => query.OrderByDescending(u => u.Role),
            ("createdat", false) => query.OrderBy(u => u.CreatedAt),
            ("createdat", true) => query.OrderByDescending(u => u.CreatedAt),
            _ => null
        };

        return (ordered ?? query.OrderByDescending(u => u.CreatedAt)).ThenBy(u => u.Id);
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
