namespace TenXEmpires.Server.Domain.Configuration;

/// <summary>
/// Settings that describe how the frontend should be referenced inside transactional emails.
/// </summary>
public sealed class FrontendSettings
{
    /// <summary>
    /// Absolute base URL for the SPA (e.g. https://play.tenxempires.com).
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>
    /// Relative path used for verification links (e.g. /verify-email).
    /// </summary>
    public string VerifyEmailPath { get; set; } = "/verify-email";

    /// <summary>
    /// Relative path used for password reset links (e.g. /reset-password).
    /// </summary>
    public string ResetPasswordPath { get; set; } = "/reset-password";

    /// <summary>
    /// Support address rendered inside templates for contact/help links.
    /// </summary>
    public string SupportEmail { get; set; } = "support@tenxempires.com";
}
