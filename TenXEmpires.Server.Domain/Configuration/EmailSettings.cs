namespace TenXEmpires.Server.Domain.Configuration;

/// <summary>
/// Email infrastructure configuration that can be supplied via appsettings or environment variables.
/// </summary>
public sealed class EmailSettings
{
    /// <summary>
    /// The display/from email address used when sending transactional emails.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Optional SMTP account/username if it differs from the from address.
    /// </summary>
    public string? Account { get; set; }

    /// <summary>
    /// Base64 encoded encryption key (256-bit) used with <see cref="Password"/>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted SMTP password produced by the encryption utility.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// SMTP host.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// SMTP port (defaults to 587 for STARTTLS).
    /// </summary>
    public int Port { get; set; } = 587;
}
