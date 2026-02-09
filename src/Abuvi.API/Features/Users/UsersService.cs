using Abuvi.API.Features.Auth;

namespace Abuvi.API.Features.Users;

/// <summary>
/// Service for handling user business logic
/// </summary>
public class UsersService(IUsersRepository repository, IPasswordHasher passwordHasher)
{
    /// <summary>
    /// Gets a user by ID
    /// </summary>
    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(id, cancellationToken);
        return user is null ? null : MapToResponse(user);
    }

    /// <summary>
    /// Gets all users with pagination
    /// </summary>
    public async Task<List<UserResponse>> GetAllAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        var users = await repository.GetAllAsync(skip, take, cancellationToken);
        return users.Select(MapToResponse).ToList();
    }

    /// <summary>
    /// Creates a new user
    /// </summary>
    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        // Check if email already exists
        if (await repository.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new InvalidOperationException("A user with this email already exists");
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Role = request.Role,
            IsActive = true
        };

        var createdUser = await repository.CreateAsync(user, cancellationToken);
        return MapToResponse(createdUser);
    }

    /// <summary>
    /// Updates an existing user
    /// </summary>
    public async Task<UserResponse?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return null;

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Phone = request.Phone;
        user.IsActive = request.IsActive;

        var updatedUser = await repository.UpdateAsync(user, cancellationToken);
        return MapToResponse(updatedUser);
    }

    /// <summary>
    /// Deletes a user
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await repository.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// Maps User entity to UserResponse DTO
    /// </summary>
    private static UserResponse MapToResponse(User user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.Phone,
        user.Role,
        user.IsActive,
        user.CreatedAt,
        user.UpdatedAt
    );
}
