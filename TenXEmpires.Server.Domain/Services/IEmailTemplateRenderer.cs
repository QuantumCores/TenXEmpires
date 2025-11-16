namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Resolves HTML templates stored on disk and applies token replacements.
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string?> tokens,
        CancellationToken cancellationToken = default);
}
