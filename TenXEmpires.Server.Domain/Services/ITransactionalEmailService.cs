namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Sends transactional emails using predefined templates.
/// </summary>
public interface ITransactionalEmailService
{
    Task SendTemplateAsync(
        string toEmail,
        string subject,
        string templateName,
        IReadOnlyDictionary<string, string?> tokens,
        CancellationToken cancellationToken = default);
}
