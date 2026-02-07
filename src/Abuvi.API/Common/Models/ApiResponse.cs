namespace Abuvi.API.Common.Models;

/// <summary>
/// Standard API response wrapper for consistent response format
/// </summary>
public record ApiResponse<T>(bool Success, T? Data = default, ApiError? Error = null)
{
    /// <summary>
    /// Creates a successful response with data
    /// </summary>
    public static ApiResponse<T> Ok(T data) => new(true, data);

    /// <summary>
    /// Creates a not found error response
    /// </summary>
    public static ApiResponse<T> NotFound(string message) =>
        new(false, Error: new ApiError(message, "NOT_FOUND"));

    /// <summary>
    /// Creates a generic error response with custom code
    /// </summary>
    public static ApiResponse<T> Fail(string message, string code) =>
        new(false, Error: new ApiError(message, code));

    /// <summary>
    /// Creates a validation error response with field details
    /// </summary>
    public static ApiResponse<T> ValidationFail(string message, List<ValidationError> details) =>
        new(false, Error: new ApiError(message, "VALIDATION_ERROR", details));
}

/// <summary>
/// Error details in API response
/// </summary>
public record ApiError(
    string Message,
    string Code,
    List<ValidationError>? Details = null
);

/// <summary>
/// Individual validation error for a specific field
/// </summary>
public record ValidationError(
    string Field,
    string Message
);
