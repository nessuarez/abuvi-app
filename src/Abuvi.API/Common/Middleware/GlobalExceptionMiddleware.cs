using Abuvi.API.Common.Models;

namespace Abuvi.API.Common.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
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
