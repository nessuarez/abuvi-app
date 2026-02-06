namespace Abuvi.API.Common.Models;

public record ApiResponse<T>(bool Success, T? Data = default, ApiError? Error = null)
{
    public static ApiResponse<T> Ok(T data) => new(true, data);

    public static ApiResponse<T> NotFound(string message) =>
        new(false, Error: new ApiError(message, "NOT_FOUND"));

    public static ApiResponse<T> Fail(string message, string code) =>
        new(false, Error: new ApiError(message, code));
}

public record ApiError(string Message, string Code);
