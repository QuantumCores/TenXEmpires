using Microsoft.Extensions.Logging.Abstractions;
using TenXEmpires.Server.Infrastructure.Security;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return;
}

var command = args[0].ToLowerInvariant();
var options = ParseOptions(args.Skip(1).ToArray());
var protector = new SecretProtector(NullLogger<SecretProtector>.Instance);

try
{
    switch (command)
    {
        case "encrypt":
            Require(options, "--key", out var encKey);
            Require(options, "--password", out var password);
            var cipher = protector.Encrypt(encKey, password);
            Console.WriteLine(cipher);
            break;
        case "decrypt":
            Require(options, "--key", out var decKey);
            Require(options, "--secret", out var secret);
            var plain = protector.Decrypt(decKey, secret);
            Console.WriteLine(plain);
            break;
        default:
            Console.Error.WriteLine($"Unknown command '{command}'.");
            PrintUsage();
            Environment.ExitCode = 1;
            break;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.ExitCode = 1;
}

static Dictionary<string, string> ParseOptions(string[] arguments)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < arguments.Length; i++)
    {
        var current = arguments[i];
        if (!current.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        if (i + 1 >= arguments.Length)
        {
            throw new ArgumentException($"Missing value for option '{current}'.");
        }

        dict[current] = arguments[i + 1];
        i++;
    }

    return dict;
}

static void Require(IReadOnlyDictionary<string, string> options, string key, out string value)
{
    if (!options.TryGetValue(key, out value!) || string.IsNullOrWhiteSpace(value))
    {
        throw new ArgumentException($"Option '{key}' is required.");
    }
}

static void PrintUsage()
{
    Console.WriteLine("""
TenX Empires Email Crypto Utility

Usage:
  emailcrypto encrypt --key <base64> --password <plain-text>
  emailcrypto decrypt --key <base64> --secret <encrypted-value>
""");
}
