using Abuvi.API.Common.Models;
using Abuvi.API.Common.Exceptions;

namespace Abuvi.API.Common.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "Resource not found: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail(ex.Message, "NOT_FOUND"));
        }
        catch (BusinessRuleException ex)
        {
            logger.LogWarning(ex, "Business rule violation: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail("An unexpected error occurred", "INTERNAL_ERROR"));
        }
    }
}
