using System.Security.Claims;

namespace Abuvi.API.Common.Extensions;

/// <summary>
/// Extension methods for HttpContext and ClaimsPrincipal
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the authenticated user's ID from JWT claims
    /// </summary>
    /// <param name="user">The claims principal from HttpContext.User</param>
    /// <returns>The user's Guid ID, or null if not found or invalid</returns>
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return null;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Gets the authenticated user's email from JWT claims
    /// </summary>
    public static string? GetUserEmail(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Gets the authenticated user's role from JWT claims
    /// </summary>
    public static string? GetUserRole(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Role)?.Value;
    }
}
