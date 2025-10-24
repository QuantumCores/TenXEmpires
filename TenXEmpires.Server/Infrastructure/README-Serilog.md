# Serilog Logging Configuration

## Overview

The TenX Empires API uses **Serilog** as its logging framework, providing structured logging with flexible output formatting and multiple sinks (Console, File, etc.).

## Configuration

Serilog is configured in `appsettings.json` and `appsettings.Development.json` files.

### Production Configuration (`appsettings.json`)

- **Log Level**: Information (with overrides for framework components)
- **Sinks**: 
  - Console (with formatted output)
  - File (rolling daily logs in `logs/` directory, 7-day retention)
- **Output Templates**: Custom templates for readability

### Development Configuration (`appsettings.Development.json`)

- **Log Level**: Debug (more verbose)
- **Sinks**: Console only (no file logging for faster development)
- **EF Core**: Shows SQL commands for debugging

## Log Levels

Serilog uses the following log levels (in order of severity):

1. **Verbose** - Most detailed, typically not used in production
2. **Debug** - Detailed information for debugging (development only)
3. **Information** - General informational messages
4. **Warning** - Warning messages for potentially harmful situations
5. **Error** - Error messages for failures
6. **Fatal** - Critical errors that cause application termination

## Using the Logger

### In Controllers and Services

The logger is injected via dependency injection using `ILogger<T>`:

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public void DoSomething()
    {
        _logger.LogInformation("Doing something");
        _logger.LogWarning("This is a warning: {Value}", someValue);
        _logger.LogError(exception, "An error occurred while processing {Id}", id);
    }
}
```

### Structured Logging

Use structured logging with properties instead of string concatenation:

```csharp
// ✅ Good - Structured
_logger.LogInformation("User {UserId} logged in at {Timestamp}", userId, DateTime.UtcNow);

// ❌ Bad - String concatenation
_logger.LogInformation($"User {userId} logged in at {DateTime.UtcNow}");
```

### Log Context Enrichment

Use `LogContext` to add properties to all logs within a scope:

```csharp
using Serilog.Context;

using (LogContext.PushProperty("RequestId", requestId))
{
    _logger.LogInformation("Processing request");
    // All logs in this scope will include RequestId
}
```

## HTTP Request Logging

Serilog automatically logs HTTP requests via `UseSerilogRequestLogging()` in `Program.cs`. This provides:

- Request method and path
- Status code
- Response time
- User identity (if authenticated)

## Log Output

### Console Output Format (Development)

```
[12:34:56 INF] TenXEmpires.Server.Controllers.GamesController: Listed 5 games for user 123e4567-e89b-12d3-a456-426614174000
```

### File Output Format (Production)

Located in `TenXEmpires.Server/logs/tenxempires-YYYYMMDD.log`:

```
2025-10-24 12:34:56.789 +00:00 [INF] TenXEmpires.Server.Controllers.GamesController: Listed 5 games for user 123e4567-e89b-12d3-a456-426614174000
```

## Configuration Options

You can modify Serilog behavior in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

## Additional Sinks

Serilog supports many sinks. Popular ones include:

- **Serilog.Sinks.Seq** - Structured log server
- **Serilog.Sinks.Elasticsearch** - Elasticsearch integration
- **Serilog.Sinks.Splunk** - Splunk integration
- **Serilog.Sinks.ApplicationInsights** - Azure Application Insights

Install via NuGet and configure in `appsettings.json`.

## Troubleshooting

**Logs not appearing?**
- Check `MinimumLevel` settings in `appsettings.json`
- Verify the environment (Development vs Production)
- Ensure `UseSerilog()` is called in `Program.cs`

**File logs not created?**
- Check file system permissions
- Verify the `path` in `appsettings.json` is valid
- Ensure the application has write access to the logs directory

**Too many logs?**
- Increase `MinimumLevel` to `Warning` or `Error`
- Add more specific overrides for noisy namespaces

## References

- [Serilog Documentation](https://serilog.net/)
- [Serilog.AspNetCore](https://github.com/serilog/serilog-aspnetcore)
- [Structured Logging Best Practices](https://github.com/serilog/serilog/wiki/Structured-Data)

