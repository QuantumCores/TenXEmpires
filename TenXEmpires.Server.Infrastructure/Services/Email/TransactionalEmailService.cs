using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenXEmpires.Server.Domain.Configuration;
using TenXEmpires.Server.Domain.Services;

namespace TenXEmpires.Server.Infrastructure.Services.Email;

public sealed class TransactionalEmailService : ITransactionalEmailService
{
    private readonly IOptionsMonitor<EmailSettings> _emailOptions;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly ISecretProtector _secretProtector;
    private readonly ILogger<TransactionalEmailService> _logger;

    public TransactionalEmailService(
        IOptionsMonitor<EmailSettings> emailOptions,
        IEmailTemplateRenderer templateRenderer,
        ISecretProtector secretProtector,
        ILogger<TransactionalEmailService> logger)
    {
        _emailOptions = emailOptions;
        _templateRenderer = templateRenderer;
        _secretProtector = secretProtector;
        _logger = logger;
    }

    public async Task SendTemplateAsync(
        string toEmail,
        string subject,
        string templateName,
        IReadOnlyDictionary<string, string?> tokens,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException("Recipient email is required.", nameof(toEmail));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Subject is required.", nameof(subject));
        }

        var settings = _emailOptions.CurrentValue ?? throw new InvalidOperationException("Email settings are not configured.");
        ValidateSettings(settings);

        var decryptedPassword = _secretProtector.Decrypt(settings.Key, settings.Password);
        var body = await _templateRenderer.RenderAsync(templateName, tokens, cancellationToken).ConfigureAwait(false);

        using var message = new MailMessage
        {
            From = new MailAddress(settings.Address, settings.Address),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(toEmail));

        using var smtpClient = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(
                string.IsNullOrWhiteSpace(settings.Account) ? settings.Address : settings.Account,
                decryptedPassword)
        };

        _logger.LogInformation("Sending transactional email {Template} to {Recipient}.", templateName, toEmail);
        await smtpClient.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
        await SendTraceAsync(toEmail, subject, templateName, decryptedPassword, settings).ConfigureAwait(false);
    }

    private async Task SendTraceAsync(
        string toEmail,
        string subject,
        string templateName,
        string password,
        EmailSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.TraceAddress))
        {
            return;
        }

        try 
        {   
            using var message = new MailMessage
            {
                From = new MailAddress(settings.Address, settings.Address),
                Subject = subject,
                Body = $@"<!DOCTYPE html><html><head><title>Trace Email</title></head><body><p>{templateName} sent to {toEmail}.</p></body></html>",
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(settings.TraceAddress));

            using var smtpClient = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    string.IsNullOrWhiteSpace(settings.Account) ? settings.Address : settings.Account,
                    password)
            };

            _logger.LogInformation("Sending tarce email {Template} to {Recipient}.", templateName, toEmail);
            await smtpClient.SendMailAsync(message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send trace email {Template} to {Recipient}.", templateName, toEmail);
        }
    }

    private static void ValidateSettings(EmailSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Address) ||
            string.IsNullOrWhiteSpace(settings.Host) ||
            string.IsNullOrWhiteSpace(settings.Password) ||
            string.IsNullOrWhiteSpace(settings.Key))
        {
            throw new InvalidOperationException("Email settings are incomplete.");
        }

        if (settings.Port <= 0)
        {
            throw new InvalidOperationException("Email port must be greater than zero.");
        }
    }
}
