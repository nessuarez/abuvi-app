namespace Abuvi.API.Features.Auth;

/// <summary>
/// BCrypt-based password hasher with configurable work factor
/// Provides secure password hashing with automatic salt generation
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12; // 2^12 = 4096 iterations

    /// <summary>
    /// Hashes a password using BCrypt with work factor 12
    /// Each hash is unique even for the same password due to random salt
    /// </summary>
    /// <param name="password">The plaintext password to hash</param>
    /// <returns>BCrypt hash string (includes salt and hash)</returns>
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);
    }

    /// <summary>
    /// Verifies a password against a BCrypt hash
    /// </summary>
    /// <param name="password">The plaintext password to verify</param>
    /// <param name="passwordHash">The BCrypt hash to verify against</param>
    /// <returns>True if password matches, false otherwise</returns>
    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
