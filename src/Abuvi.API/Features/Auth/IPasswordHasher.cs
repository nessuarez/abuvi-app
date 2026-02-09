namespace Abuvi.API.Features.Auth;

/// <summary>
/// Provides password hashing and verification using BCrypt
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password using BCrypt with salt
    /// </summary>
    /// <param name="password">The plaintext password to hash</param>
    /// <returns>BCrypt hashed password string</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a BCrypt hash
    /// </summary>
    /// <param name="password">The plaintext password to verify</param>
    /// <param name="passwordHash">The BCrypt hash to verify against</param>
    /// <returns>True if password matches hash, false otherwise</returns>
    bool VerifyPassword(string password, string passwordHash);
}
