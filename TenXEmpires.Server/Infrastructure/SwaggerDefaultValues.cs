using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TenXEmpires.Server.Infrastructure;

/// <summary>
/// Represents the Swagger/Swashbuckle operation filter used to document API versions.
/// </summary>
public class SwaggerDefaultValues : IOperationFilter
{
    /// <summary>
    /// Applies the filter to the specified operation using the given context.
    /// </summary>
    /// <param name="operation">The operation to apply the filter to.</param>
    /// <param name="context">The current operation filter context.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;

        // Mark deprecated operations
        foreach (var metadata in apiDescription.ActionDescriptor.EndpointMetadata)
        {
            if (metadata is ObsoleteAttribute)
            {
                operation.Deprecated = true;
                break;
            }
        }

        if (operation.Parameters != null)
        {
            foreach (var parameter in operation.Parameters)
            {
                var description = apiDescription.ParameterDescriptions
                    .FirstOrDefault(p => p.Name == parameter.Name);

                if (description == null)
                {
                    continue;
                }

                parameter.Description ??= description.ModelMetadata?.Description;
                parameter.Required |= description.IsRequired;
            }
        }

        // Add examples and response headers for specific endpoints
        var action = context.MethodInfo;
        var controller = action.DeclaringType?.Name;

        if (controller == nameof(Server.Controllers.MapsController) && action.Name == "GetMapByCode")
        {
            if (operation.Responses.TryGetValue("200", out var ok))
            {
                // Example body for MapDto
                if (ok.Content.TryGetValue("application/json", out var media))
                {
                    media.Example = new OpenApiObject
                    {
                        ["id"] = new OpenApiInteger(1),
                        ["code"] = new OpenApiString("map-01"),
                        ["schemaVersion"] = new OpenApiInteger(1),
                        ["width"] = new OpenApiInteger(20),
                        ["height"] = new OpenApiInteger(30)
                    };
                }

                // ETag response header
                ok.Headers["ETag"] = new OpenApiHeader
                {
                    Description = "Entity tag for cache validation",
                    Schema = new OpenApiSchema { Type = "string" },
                    Example = new OpenApiString("\"abc123def\"")
                };
            }

            if (operation.Responses.TryGetValue("304", out var notModified))
            {
                notModified.Headers["ETag"] = new OpenApiHeader
                {
                    Description = "Entity tag for cache validation",
                    Schema = new OpenApiSchema { Type = "string" },
                    Example = new OpenApiString("\"abc123def\"")
                };
            }
        }

        if (controller == nameof(Server.Controllers.UnitDefinitionsController) && action.Name == "GetUnitDefinitions")
        {
            if (operation.Responses.TryGetValue("200", out var ok))
            {
                if (ok.Content.TryGetValue("application/json", out var media))
                {
                    // Example body for ItemsResult<UnitDefinitionDto>
                    media.Example = new OpenApiObject
                    {
                        ["items"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["id"] = new OpenApiInteger(1),
                                ["code"] = new OpenApiString("warrior"),
                                ["isRanged"] = new OpenApiBoolean(false),
                                ["attack"] = new OpenApiInteger(20),
                                ["defence"] = new OpenApiInteger(10),
                                ["rangeMin"] = new OpenApiInteger(0),
                                ["rangeMax"] = new OpenApiInteger(0),
                                ["movePoints"] = new OpenApiInteger(2),
                                ["health"] = new OpenApiInteger(100)
                            },
                            new OpenApiObject
                            {
                                ["id"] = new OpenApiInteger(2),
                                ["code"] = new OpenApiString("archer"),
                                ["isRanged"] = new OpenApiBoolean(true),
                                ["attack"] = new OpenApiInteger(15),
                                ["defence"] = new OpenApiInteger(5),
                                ["rangeMin"] = new OpenApiInteger(2),
                                ["rangeMax"] = new OpenApiInteger(3),
                                ["movePoints"] = new OpenApiInteger(2),
                                ["health"] = new OpenApiInteger(80)
                            }
                        }
                    };
                }

                ok.Headers["ETag"] = new OpenApiHeader
                {
                    Description = "Entity tag for cache validation",
                    Schema = new OpenApiSchema { Type = "string" },
                    Example = new OpenApiString("\"unitdefs-etag\"")
                };
            }

            if (operation.Responses.TryGetValue("304", out var notModified))
            {
                notModified.Headers["ETag"] = new OpenApiHeader
                {
                    Description = "Entity tag for cache validation",
                    Schema = new OpenApiSchema { Type = "string" },
                    Example = new OpenApiString("\"unitdefs-etag\"")
                };
            }
        }
    }
}

