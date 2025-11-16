namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Provides authenticated encryption utilities for protecting sensitive secrets at rest.
/// </summary>
public interface ISecretProtector
{
    /// <summary>
    /// Encrypts <paramref name="plainText"/> using a base64-encoded key.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when key is invalid.</exception>
    string Encrypt(string base64Key, string plainText);

    /// <summary>
    /// Decrypts <paramref name="cipherText"/> that was previously produced by <see cref="Encrypt"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when payload has been tampered with or key is wrong.</exception>
    string Decrypt(string base64Key, string cipherText);
}
