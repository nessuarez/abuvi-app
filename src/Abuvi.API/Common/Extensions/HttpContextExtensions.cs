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

    /// <summary>
    /// True when the caller is Admin or Board.
    ///
    /// Used to widen what a response contains rather than to gate access — moderator-only
    /// endpoints are protected by RequireRole. Passing this into a service lets the mapper
    /// strip privileged fields (contributor contact details, full source paths) server-side,
    /// which is the only place that decision can safely be made.
    /// </summary>
    public static bool IsAdminOrBoard(this ClaimsPrincipal user)
        => user.IsInRole("Admin") || user.IsInRole("Board");
}
