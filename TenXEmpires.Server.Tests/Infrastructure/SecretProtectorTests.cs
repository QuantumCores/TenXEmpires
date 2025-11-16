using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TenXEmpires.Server.Infrastructure.Security;

namespace TenXEmpires.Server.Tests.Infrastructure;

public sealed class SecretProtectorTests
{
    private static SecretProtector CreateProtector() =>
        new(NullLogger<SecretProtector>.Instance);

    [Fact]
    public void EncryptDecrypt_RoundTripsSuccessfully()
    {
        var protector = CreateProtector();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        const string plainText = "Tr!cky-P@ssw0rd";

        var cipher = protector.Encrypt(key, plainText);
        cipher.Should().NotBeNullOrWhiteSpace().And.NotContain(plainText);

        var decrypted = protector.Decrypt(key, cipher);
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void Encrypt_WithInvalidKey_ThrowsArgumentException()
    {
        var protector = CreateProtector();
        Action act = () => protector.Encrypt("dGVzdA==", "value"); // 4 byte key
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Decrypt_WithTamperedPayload_ThrowsInvalidOperationException()
    {
        var protector = CreateProtector();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var cipher = protector.Encrypt(key, "payload");

        var bytes = Convert.FromBase64String(cipher);
        bytes[^1] ^= 0xFF; // flip last bit
        var tampered = Convert.ToBase64String(bytes);

        Action act = () => protector.Decrypt(key, tampered);
        act.Should().Throw<InvalidOperationException>();
    }
}
