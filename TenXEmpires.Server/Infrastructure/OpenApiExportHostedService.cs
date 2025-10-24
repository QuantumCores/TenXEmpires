using Swashbuckle.AspNetCore.Swagger;

namespace TenXEmpires.Server.Infrastructure;

/// <summary>
/// Background service that exports OpenAPI specification to YAML on application startup.
/// Only runs in Development environment.
/// </summary>
public class OpenApiExportHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OpenApiExportHostedService> _logger;

    public OpenApiExportHostedService(
        IServiceProvider serviceProvider,
        IWebHostEnvironment environment,
        ILogger<OpenApiExportHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Only export in development
        if (!_environment.IsDevelopment())
        {
            _logger.LogDebug("OpenAPI auto-export skipped (not in Development environment)");
            return;
        }

        try
        {
            // Give the app a moment to fully initialize Swagger
            await Task.Delay(2000, cancellationToken);

            using var scope = _serviceProvider.CreateScope();
            var swaggerProvider = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>();
            var exporter = scope.ServiceProvider.GetRequiredService<OpenApiExporter>();

            // Generate the OpenAPI document
            var swagger = swaggerProvider.GetSwagger("v1");

            // Export to docs/openapi.yaml
            var projectRoot = Directory.GetCurrentDirectory();
            var outputPath = Path.Combine(projectRoot, "..", "docs", "openapi.yaml");
            outputPath = Path.GetFullPath(outputPath);

            await exporter.ExportToYamlAsync(swagger, outputPath);

            _logger.LogInformation("✅ OpenAPI spec auto-exported on startup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-export OpenAPI spec on startup");
            // Don't throw - this shouldn't prevent the app from starting
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

