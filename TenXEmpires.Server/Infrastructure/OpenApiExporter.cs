using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TenXEmpires.Server.Infrastructure;

/// <summary>
/// Service for exporting OpenAPI specifications to YAML format.
/// </summary>
public class OpenApiExporter
{
    private readonly ILogger<OpenApiExporter> _logger;

    public OpenApiExporter(ILogger<OpenApiExporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Exports the OpenAPI document to a YAML file.
    /// </summary>
    /// <param name="document">The OpenAPI document to export.</param>
    /// <param name="outputPath">The file path where the YAML should be saved.</param>
    public async Task ExportToYamlAsync(OpenApiDocument document, string outputPath)
    {
        try
        {
            // First, convert OpenAPI document to JSON
            using var stringWriter = new StringWriter();
            var jsonWriter = new OpenApiJsonWriter(stringWriter);
            document.SerializeAsV3(jsonWriter);
            var jsonContent = stringWriter.ToString();

            // Parse JSON and convert to YAML
            var deserializer = new DeserializerBuilder()
                .Build();
            var yamlObject = deserializer.Deserialize(new StringReader(jsonContent));

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var yamlContent = serializer.Serialize(yamlObject);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to file
            await File.WriteAllTextAsync(outputPath, yamlContent);

            _logger.LogInformation("OpenAPI spec exported to: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export OpenAPI spec to {OutputPath}", outputPath);
            throw;
        }
    }
}

