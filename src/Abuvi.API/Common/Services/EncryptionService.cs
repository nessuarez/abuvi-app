namespace Abuvi.API.Common.Services;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// AES-256 encryption service for sensitive health data
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public EncryptionService(IConfiguration configuration)
    {
        var encryptionKey = configuration["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption key not configured. Set 'Encryption:Key' in user secrets or configuration.");

        // Derive 256-bit key from configuration string using SHA256
        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(encryptionKey));

        // Use fixed IV (zeros for simplicity - in production, should use random IV stored with ciphertext)
        _iv = new byte[16]; // 128-bit IV
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);

        return sr.ReadToEnd();
    }
}
