using System.IO;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Serilog;

namespace TenXEmpires.Server.Infrastructure;

/// <summary>
/// Service to run database migrations on application startup.
/// Uses raw Npgsql to avoid version conflicts with DbUp.
/// </summary>
public class DatabaseMigrationService
{
    private readonly IConfiguration _configuration;

    public DatabaseMigrationService(IConfiguration configuration)
    {
        _configuration = configuration;
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
            Log.Error("Connection string 'DefaultConnection' is not configured");
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
                Log.Warning("Migrations directory not found at {MigrationsPath}. Skipping migrations.", migrationsPath);
                return true; // Don't fail startup if migrations directory doesn't exist
            }
        }

        var migrationFiles = Directory.GetFiles(migrationsPath, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(f => Path.GetFileName(f))
            .ToArray();

        if (migrationFiles.Length == 0)
        {
            Log.Information("No migration scripts found in {MigrationsPath}. Skipping migrations.", migrationsPath);
            return true;
        }

        try
        {
            Log.Information("Starting database migrations from {MigrationsPath}. Found {Count} migration file(s).", migrationsPath, migrationFiles.Length);

            // Parse connection string to get database name
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var databaseName = builder.Database;

            if (ensureDatabase)
            {
                Log.Information("Ensuring database exists...");
                EnsureDatabaseExists(builder);
            }

            // Check which migrations have already been applied
            var appliedMigrations = GetAppliedMigrations(connectionString);

            foreach (var migrationFile in migrationFiles)
            {
                var fileName = Path.GetFileName(migrationFile);
                if (appliedMigrations.Contains(fileName))
                {
                    Log.Debug("Migration {FileName} already applied, skipping.", fileName);
                    continue;
                }

                Log.Information("Executing migration: {FileName}", fileName);
                var sql = File.ReadAllText(migrationFile);

                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                using var transaction = connection.BeginTransaction();
                try
                {
                    // Execute the migration script
                    using var command = new NpgsqlCommand(sql, connection, transaction);
                    command.CommandTimeout = 300; // 5 minutes timeout
                    command.ExecuteNonQuery();

                    // Record the migration in the journal table
                    RecordMigration(connection, transaction, fileName);

                    transaction.Commit();
                    Log.Information("Migration {FileName} completed successfully.", fileName);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Log.Error(ex, "Migration {FileName} failed. Rolling back.", fileName);
                    return false;
                }
            }

            Log.Information("Database migrations completed successfully. {Count} migration(s) executed.", 
                migrationFiles.Length - appliedMigrations.Count);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error running database migrations");
            return false;
        }
    }

    private void EnsureDatabaseExists(NpgsqlConnectionStringBuilder builder)
    {
        var databaseName = builder.Database;
        builder.Database = "postgres"; // Connect to default database

        using var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();

        var checkDbCommand = new NpgsqlCommand(
            $"SELECT 1 FROM pg_database WHERE datname = '{databaseName}'",
            connection);
        var exists = checkDbCommand.ExecuteScalar() != null;

        if (!exists)
        {
            Log.Information("Creating database {DatabaseName}...", databaseName);
            var createDbCommand = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            createDbCommand.ExecuteNonQuery();
            Log.Information("Database {DatabaseName} created successfully.", databaseName);
        }
    }

    private HashSet<string> GetAppliedMigrations(string connectionString)
    {
        var appliedMigrations = new HashSet<string>();

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            // Ensure the journal table exists
            EnsureJournalTable(connection);

            var command = new NpgsqlCommand(
                "SELECT \"ScriptName\" FROM public.\"schemaversions\" ORDER BY \"ScriptName\"",
                connection);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                appliedMigrations.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not read applied migrations. Assuming none are applied.");
        }

        return appliedMigrations;
    }

    private void EnsureJournalTable(NpgsqlConnection connection)
    {
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS public.""schemaversions""
            (
                ""Id"" SERIAL PRIMARY KEY,
                ""ScriptName"" VARCHAR(255) NOT NULL UNIQUE,
                ""Applied"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );";

        using var command = new NpgsqlCommand(createTableSql, connection);
        command.ExecuteNonQuery();
    }

    private void RecordMigration(NpgsqlConnection connection, NpgsqlTransaction transaction, string fileName)
    {
        var insertSql = @"
            INSERT INTO public.""schemaversions"" (""ScriptName"", ""Applied"")
            VALUES (@scriptName, CURRENT_TIMESTAMP)
            ON CONFLICT (""ScriptName"") DO NOTHING;";

        using var command = new NpgsqlCommand(insertSql, connection, transaction);
        command.Parameters.AddWithValue("scriptName", fileName);
        command.ExecuteNonQuery();
    }
}
