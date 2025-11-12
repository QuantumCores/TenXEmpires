using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DbUp;
using DbUp.Engine.Output;

namespace TenX.DbMigrate
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // Simple arg parsing
            var argDict = ParseArgs(args);
            var scriptsPath = argDict.TryGetValue("--scripts", out var s) ? s : Path.Combine(Environment.CurrentDirectory, "db", "migrations");
            var preview = argDict.ContainsKey("--preview");
            var ensureDb = argDict.ContainsKey("--ensure-database");
            var timeoutSeconds = argDict.TryGetValue("--timeout", out var t) && int.TryParse(t, out var ts) ? ts : 0;

            var conn = ResolveConnectionString(argDict);
            if (string.IsNullOrWhiteSpace(conn))
            {
                Console.Error.WriteLine("Missing connection string. Provide --connection or set TENX_DB_CONNECTION / POSTGRES_CONNECTION_STRING / DATABASE_URL.");
                return 2;
            }

            if (!Directory.Exists(scriptsPath))
            {
                Console.Error.WriteLine($"Scripts path not found: {scriptsPath}");
                return 3;
            }

            if (ensureDb)
            {
                try
                {
                    DbUp.EnsureDatabase.For.PostgresqlDatabase(conn);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"EnsureDatabase failed: {e.Message}");
                    return 4;
                }
            }

            if (!Directory.GetFiles(scriptsPath, "*.sql", SearchOption.TopDirectoryOnly).Any())
            {
                Console.WriteLine($"No scripts found in {scriptsPath}");
                return 0;
            }

            var log = new ConsoleUpgradeLog();
            var upgrader = DeployChanges.To
                .PostgresqlDatabase(conn)
                .WithScriptsFromFileSystem(scriptsPath)
                .LogTo(log)
                // Use public schema for the journal to avoid bootstrap dependency
                // on the app schema before the first migration creates it.
                .JournalToPostgresqlTable("public", "schemaversions")
                .Build();

            if (preview)
            {
                Console.WriteLine("-- Preview mode: listing pending scripts --");
                var executed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    executed = upgrader.GetExecutedScripts().ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex) when (ex.Message.Contains("does not exist") || ex.Message.Contains("database"))
                {
                    Console.WriteLine("Note: Database does not exist yet - all scripts will be marked as pending.");
                }
                
                var scriptFiles = Directory
                    .GetFiles(scriptsPath, "*.sql", SearchOption.TopDirectoryOnly)
                    .OrderBy(Path.GetFileName);
                    
                foreach (var fileName in scriptFiles.Select(Path.GetFileName))
                {
                    if (fileName is null) continue;
                    var marker = executed.Contains(fileName) ? "[ applied ]" : "[ pending ]";
                    Console.WriteLine($"{marker} {fileName}");
                }
                return 0;
            }

            if (timeoutSeconds > 0)
            {
                // DbUp doesn't expose a direct command timeout setter for PostgreSQL.
                // For long migrations, prefer splitting scripts or setting statement timeouts inside SQL.
                Console.WriteLine($"Note: --timeout {timeoutSeconds}s provided; consider using SET statement_timeout in scripts.");
            }

            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
            {
                Console.Error.WriteLine(result.Error);
                return -1;
            }

            Console.WriteLine("Migration completed successfully.");
            return 0;
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (!a.StartsWith("-"))
                {
                    continue;
                }

                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    d[a] = args[i + 1];
                    i++;
                }
                else
                {
                    d[a] = string.Empty;
                }
            }
            return d;
        }

        private static string ResolveConnectionString(Dictionary<string, string> argDict)
        {
            if (argDict.TryGetValue("--connection", out var conn) && !string.IsNullOrWhiteSpace(conn))
            { 
                var isUrl = conn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) 
                    || conn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
                
                Console.WriteLine($"Connection from --connection argument: {(isUrl ? "URL format" : "Connection string format")}");
                
                if (isUrl)
                {
                    var parsed = ParseDatabaseUrl(conn);
                    if (parsed != null)
                    {
                        Console.WriteLine("Successfully parsed URL to connection string format");
                        return parsed;
                    }
                    Console.WriteLine("Warning: Failed to parse URL, using original value");
                    return conn;
                }
                return conn;
            }

            var tenxConn = Environment.GetEnvironmentVariable("TENX_DB_CONNECTION");
            if (!string.IsNullOrWhiteSpace(tenxConn))
            {
                var isUrl = tenxConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) 
                    || tenxConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
                
                Console.WriteLine($"TENX_DB_CONNECTION found: {(isUrl ? "URL format" : "Connection string format")}");
                
                if (isUrl)
                {
                    var parsed = ParseDatabaseUrl(tenxConn);
                    if (parsed != null)
                    {
                        Console.WriteLine("Successfully parsed URL to connection string format");
                        return parsed;
                    }
                    Console.WriteLine("Warning: Failed to parse URL, using original value");
                    return tenxConn;
                }
                return tenxConn;
            }

            var postgresConn = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(postgresConn))
            {
                var isUrl = postgresConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) 
                    || postgresConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
                
                Console.WriteLine($"POSTGRES_CONNECTION_STRING found: {(isUrl ? "URL format" : "Connection string format")}");
                
                if (isUrl)
                {
                    var parsed = ParseDatabaseUrl(postgresConn);
                    if (parsed != null)
                    {
                        Console.WriteLine("Successfully parsed URL to connection string format");
                        return parsed;
                    }
                    Console.WriteLine("Warning: Failed to parse URL, using original value");
                    return postgresConn;
                }
                return postgresConn;
            }

            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrWhiteSpace(databaseUrl))
            {
                Console.WriteLine("DATABASE_URL found: URL format");
                var parsed = ParseDatabaseUrl(databaseUrl);
                if (parsed != null)
                {
                    Console.WriteLine("Successfully parsed URL to connection string format");
                    return parsed;
                }
                Console.WriteLine("Warning: Failed to parse URL, using original value");
                return databaseUrl;
            }

            return string.Empty;
        }

        private static string? ParseDatabaseUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            // Expect postgres://user:pass@host:port/db?sslmode=require
            try
            {
                var u = new Uri(url);
                var userInfo = u.UserInfo.Split(':');
                var user = Uri.UnescapeDataString(userInfo[0]);
                var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
                var host = u.Host;
                var port = u.Port > 0 ? u.Port.ToString(CultureInfo.InvariantCulture) : "5432";
                var db = u.AbsolutePath.Trim('/');
                var q = ParseQuery(u.Query);
                q.TryGetValue("sslmode", out var sslmodeVal);
                var sslmode = string.IsNullOrWhiteSpace(sslmodeVal) ? "Prefer" : sslmodeVal;
                return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode={sslmode};";
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<string, string> ParseQuery(string? query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
            {
                return dict;
            }

            var q = query;
            if (q.StartsWith("?"))
            {
                q = q.Substring(1);
            }

            foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(kv[0]);
                var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                dict[key] = val;
            }
            return dict;
        }
    }
}
