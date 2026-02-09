using Abuvi.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Repository for UserRoleChangeLog audit operations
/// </summary>
public class UserRoleChangeLogsRepository(AbuviDbContext context) : IUserRoleChangeLogsRepository
{
    public async Task LogRoleChangeAsync(UserRoleChangeLog log, CancellationToken cancellationToken = default)
    {
        context.UserRoleChangeLogs.Add(log);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<UserRoleChangeLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.UserRoleChangeLogs
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.ChangedAt)
            .ToListAsync(cancellationToken);
    }
}
