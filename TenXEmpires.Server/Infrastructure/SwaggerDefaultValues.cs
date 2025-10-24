using Microsoft.OpenApi.Models;
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

        if (operation.Parameters == null)
        {
            return;
        }

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
}

