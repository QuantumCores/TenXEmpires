# OpenAPI YAML Auto-Export

## Overview

The OpenAPI specification is automatically exported to `docs/openapi.yaml` whenever you run the application in **Development** mode.

## How It Works

1. **OpenApiExporter.cs** - Service that converts the Swagger JSON spec to YAML format using YamlDotNet
2. **OpenApiExportHostedService.cs** - Background service that runs on application startup (Development only)
3. Exports after a 2-second delay to ensure Swagger is fully initialized
4. Output location: `docs/openapi.yaml` (relative to solution root)

## Usage

### Automatic Export (Recommended)

Simply run the application in Development mode:

```bash
dotnet run --project TenXEmpires.Server/TenXEmpires.Server.csproj
```

The `docs/openapi.yaml` file will be automatically updated on startup.

### What Gets Exported

- All API endpoints with full documentation
- Request/response schemas
- Example responses (from `IExamplesProvider` implementations)
- Validation rules from DataAnnotations
- XML comments from controller actions

## Benefits

✅ **No Manual Maintenance** - YAML stays in sync with code automatically  
✅ **Single Source of Truth** - Code annotations drive the documentation  
✅ **Rich Examples** - Custom examples from `IExamplesProvider` classes  
✅ **Type Safety** - Schemas generated from actual DTOs  
✅ **Development-Only** - No performance impact in production  

## Adding Examples

To add or customize examples for endpoints:

1. Create a class implementing `IExamplesProvider<T>` in `TenXEmpires.Server/Examples/`
2. Add `[SwaggerResponseExample(StatusCode, typeof(YourExampleClass))]` to the controller action
3. The example will automatically appear in the exported YAML

Example:

```csharp
public class MyDtoExample : IExamplesProvider<MyDto>
{
    public MyDto GetExamples()
    {
        return new MyDto(/* example data */);
    }
}

// In controller:
[SwaggerResponseExample(StatusCodes.Status200OK, typeof(MyDtoExample))]
public async Task<ActionResult<MyDto>> GetSomething()
{
    // ...
}
```

## Troubleshooting

**YAML not generated?**
- Check that you're running in Development environment
- Look for log message: "✅ OpenAPI spec auto-exported on startup"
- Check application logs for export errors

**YAML looks wrong?**
- Ensure all controller actions have XML comments
- Verify `ProducesResponseType` attributes are correct
- Check that example providers are registered in `Program.cs`

## Production Considerations

- Auto-export **only runs in Development** (via environment check)
- No dependencies are needed in production
- The `openapi.yaml` file can be committed to version control for external consumers

