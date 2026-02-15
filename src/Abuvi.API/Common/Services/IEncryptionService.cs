namespace Abuvi.API.Common.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive data (medical notes, allergies)
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext using AES-256
    /// </summary>
    /// <param name="plainText">Text to encrypt</param>
    /// <returns>Base64-encoded ciphertext</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts ciphertext using AES-256
    /// </summary>
    /// <param name="cipherText">Base64-encoded ciphertext</param>
    /// <returns>Decrypted plaintext</returns>
    string Decrypt(string cipherText);
}
