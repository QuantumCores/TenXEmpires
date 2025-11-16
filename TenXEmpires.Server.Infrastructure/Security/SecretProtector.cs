using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Domain.Services;

namespace TenXEmpires.Server.Infrastructure.Security;

/// <summary>
/// AES-GCM based secret protector used for encrypting SMTP credentials.
/// </summary>
public sealed class SecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const byte CurrentVersion = 1;
    private const int Aes256KeySize = 32;

    private readonly ILogger<SecretProtector> _logger;

    public SecretProtector(ILogger<SecretProtector> logger)
    {
        _logger = logger;
    }

    public string Encrypt(string base64Key, string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new ArgumentException("Plain text cannot be empty.", nameof(plainText));
        }

        var keyBytes = DecodeAndValidateKey(base64Key);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        try
        {
            using var aes = new AesGcm(keyBytes, TagSize);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

            var payload = new byte[1 + NonceSize + TagSize + cipherBytes.Length];
            payload[0] = CurrentVersion;
            Buffer.BlockCopy(nonce, 0, payload, 1, NonceSize);
            Buffer.BlockCopy(tag, 0, payload, 1 + NonceSize, TagSize);
            Buffer.BlockCopy(cipherBytes, 0, payload, 1 + NonceSize + TagSize, cipherBytes.Length);

            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            CryptographicOperations.ZeroMemory(cipherBytes);
        }
    }

    public string Decrypt(string base64Key, string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            throw new ArgumentException("Cipher text cannot be empty.", nameof(cipherText));
        }

        var payload = Convert.FromBase64String(cipherText);
        if (payload.Length < 1 + NonceSize + TagSize)
        {
            throw new InvalidOperationException("Encrypted payload is invalid.");
        }

        var version = payload[0];
        if (version != CurrentVersion)
        {
            throw new InvalidOperationException($"Unknown payload version: {version}.");
        }

        var nonce = new byte[NonceSize];
        Buffer.BlockCopy(payload, 1, nonce, 0, NonceSize);
        var tag = new byte[TagSize];
        Buffer.BlockCopy(payload, 1 + NonceSize, tag, 0, TagSize);
        var cipherLength = payload.Length - (1 + NonceSize + TagSize);
        var cipherBytes = new byte[cipherLength];
        Buffer.BlockCopy(payload, 1 + NonceSize + TagSize, cipherBytes, 0, cipherLength);

        var keyBytes = DecodeAndValidateKey(base64Key);
        var plainBytes = new byte[cipherLength];

        try
        {
            using var aes = new AesGcm(keyBytes, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt payload due to invalid key or tampering.");
            throw new InvalidOperationException("Unable to decrypt payload.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cipherBytes);
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    private static byte[] DecodeAndValidateKey(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            throw new ArgumentException("Encryption key cannot be empty.", nameof(base64Key));
        }

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(base64Key);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Encryption key must be a valid base64 string.", nameof(base64Key), ex);
        }

        if (keyBytes.Length != Aes256KeySize)
        {
            throw new ArgumentException("Encryption key must be 256-bit (32 bytes).", nameof(base64Key));
        }

        return keyBytes;
    }
}
