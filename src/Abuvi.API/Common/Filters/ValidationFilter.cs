using Abuvi.API.Common.Models;
using FluentValidation;

namespace Abuvi.API.Common.Filters;

/// <summary>
/// Endpoint filter that automatically validates request DTOs using FluentValidation
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Get validator from DI container
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
            return await next(context);

        // Extract request from endpoint arguments
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
            return await next(context);

        // Validate request
        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (!result.IsValid)
        {
            // Convert FluentValidation errors to our format
            var errors = result.Errors
                .Select(e => new ValidationError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return Results.BadRequest(
                ApiResponse<object>.ValidationFail("Validation failed", errors)
            );
        }

        return await next(context);
    }
}
