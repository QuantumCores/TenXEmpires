using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Domain.Services;

namespace TenXEmpires.Server.Infrastructure.Services.Email;

/// <summary>
/// Loads HTML templates from EmailTemplates/ and applies {{Token}} replacements.
/// </summary>
public sealed class FileSystemEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly ILogger<FileSystemEmailTemplateRenderer> _logger;
    private readonly IFileProvider _fileProvider;
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _templateCache = new(StringComparer.OrdinalIgnoreCase);

    public FileSystemEmailTemplateRenderer(
        IWebHostEnvironment environment,
        ILogger<FileSystemEmailTemplateRenderer> logger)
    {
        _logger = logger;
        _fileProvider = environment.ContentRootFileProvider;
    }

    public async Task<string> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string?> tokens,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            throw new ArgumentException("Template name is required.", nameof(templateName));
        }

        var template = await GetTemplateAsync(templateName, cancellationToken).ConfigureAwait(false);
        var rendered = template;

        if (tokens is not null)
        {
            foreach (var kvp in tokens)
            {
                var placeholder = "{{" + kvp.Key + "}}";
                var replacement = kvp.Value is null ? string.Empty : WebUtility.HtmlEncode(kvp.Value);
                rendered = rendered.Replace(placeholder, replacement, StringComparison.OrdinalIgnoreCase);
            }
        }

        return rendered;
    }

    private Task<string> GetTemplateAsync(string templateName, CancellationToken cancellationToken)
    {
        var normalizedName = templateName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? templateName
            : $"{templateName}.html";

        var lazyTemplate = _templateCache.GetOrAdd(
            normalizedName,
            name => new Lazy<Task<string>>(() => LoadTemplateFromDiskAsync(name, cancellationToken)));

        return lazyTemplate.Value;
    }

    private async Task<string> LoadTemplateFromDiskAsync(string fileName, CancellationToken cancellationToken)
    {
        var fileInfo = _fileProvider.GetFileInfo(Path.Combine("EmailTemplates", fileName));
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"Email template '{fileName}' was not found.", fileName);
        }

        await using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Loaded email template {TemplateName}", fileName);
        return content;
    }
}
