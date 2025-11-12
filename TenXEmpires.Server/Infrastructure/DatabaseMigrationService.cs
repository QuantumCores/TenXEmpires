using System.IO;
using DbUp;
using DbUp.Engine.Output;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TenXEmpires.Server.Infrastructure;

/// <summary>
/// Service to run database migrations on application startup.
/// </summary>
public class DatabaseMigrationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(IConfiguration configuration, ILogger<DatabaseMigrationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Runs database migrations from the migrations directory.
    /// </summary>
    /// <param name="ensureDatabase">If true, creates the database if it doesn't exist.</param>
    /// <returns>True if migrations succeeded, false otherwise.</returns>
    public bool RunMigrations(bool ensureDatabase = false)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("Connection string 'DefaultConnection' is not configured");
            return false;
        }

        // Determine migrations path - check if db/migrations exists relative to app directory
        var migrationsPath = Path.Combine(AppContext.BaseDirectory, "db", "migrations");
        if (!Directory.Exists(migrationsPath))
        {
            // Try parent directory (for when running from bin/Debug or bin/Release)
            var parentMigrationsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "db", "migrations");
            if (Directory.Exists(parentMigrationsPath))
            {
                migrationsPath = Path.GetFullPath(parentMigrationsPath);
            }
            else
            {
                _logger.LogWarning("Migrations directory not found at {MigrationsPath}. Skipping migrations.", migrationsPath);
                return true; // Don't fail startup if migrations directory doesn't exist
            }
        }

        if (!Directory.GetFiles(migrationsPath, "*.sql", SearchOption.TopDirectoryOnly).Any())
        {
            _logger.LogInformation("No migration scripts found in {MigrationsPath}. Skipping migrations.", migrationsPath);
            return true;
        }

        try
        {
            _logger.LogInformation("Starting database migrations from {MigrationsPath}", migrationsPath);

            if (ensureDatabase)
            {
                _logger.LogInformation("Ensuring database exists...");
                EnsureDatabase.For.PostgresqlDatabase(connectionString);
            }

            var log = new SerilogUpgradeLog(_logger);
            var upgrader = DeployChanges.To
                .PostgresqlDatabase(connectionString)
                .WithScriptsFromFileSystem(migrationsPath)
                .LogTo(log)
                .JournalToPostgresqlTable("public", "schemaversions")
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                _logger.LogError(result.Error, "Database migration failed");
                return false;
            }

            _logger.LogInformation("Database migrations completed successfully. {ScriptsExecuted} script(s) executed.", 
                result.Scripts.Count());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running database migrations");
            return false;
        }
    }

    /// <summary>
    /// Custom DbUp log adapter that uses Serilog/ILogger.
    /// </summary>
    private class SerilogUpgradeLog : IUpgradeLog
    {
        private readonly ILogger<DatabaseMigrationService> _logger;

        public SerilogUpgradeLog(ILogger<DatabaseMigrationService> logger)
        {
            _logger = logger;
        }

        public void WriteInformation(string format, params object[] args)
        {
            _logger.LogInformation(format, args);
        }

        public void WriteError(string format, params object[] args)
        {
            _logger.LogError(format, args);
        }

        public void WriteWarning(string format, params object[] args)
        {
            _logger.LogWarning(format, args);
        }

        public void LogTrace(string format, params object[] args)
        {
            _logger.LogTrace(format, args);
        }

        public void LogDebug(string format, params object[] args)
        {
            _logger.LogDebug(format, args);
        }

        public void LogInformation(string format, params object[] args)
        {
            _logger.LogInformation(format, args);
        }

        public void LogWarning(string format, params object[] args)
        {
            _logger.LogWarning(format, args);
        }

        public void LogError(string format, params object[] args)
        {
            _logger.LogError(format, args);
        }

        public void LogError(Exception ex, string format, params object[] args)
        {
            _logger.LogError(ex, format, args);
        }
    }
}

