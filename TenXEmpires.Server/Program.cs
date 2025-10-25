using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Middleware;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Configure Serilog early for bootstrap logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting TenX Empires API");

                var builder = WebApplication.CreateBuilder(args);

                // Configure Serilog from appsettings
                builder.Host.UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext());

            // Add services to the container.

            // Register DbContext
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<TenXDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Register configuration settings
            builder.Services.Configure<TenXEmpires.Server.Domain.Configuration.GameSettings>(
                builder.Configuration.GetSection("GameSettings"));

            // Register application services
            builder.Services.AddScoped<ILookupService, LookupService>();
            builder.Services.AddScoped<IGameService, GameService>();
            builder.Services.AddScoped<IGameSeedingService, GameSeedingService>();
            builder.Services.AddScoped<IGameStateService, GameStateService>();
            builder.Services.AddScoped<ITurnService, TurnService>();
            builder.Services.AddScoped<IActionService, ActionService>();
            builder.Services.AddScoped<ISaveService, SaveService>();
            builder.Services.AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>();
            builder.Services.AddSingleton<IAiNameGenerator, AiNameGenerator>();

            // Add memory cache for caching lookup data
            builder.Services.AddMemoryCache();

            // Configure CORS
            var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            var corsAllowCredentials = builder.Configuration.GetValue<bool>("Cors:AllowCredentials", true);
            var corsAllowedMethods = builder.Configuration.GetSection("Cors:AllowedMethods").Get<string[]>() ?? new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
            var corsAllowedHeaders = builder.Configuration.GetSection("Cors:AllowedHeaders").Get<string[]>() ?? new[] { "*" };
            var corsExposedHeaders = builder.Configuration.GetSection("Cors:ExposedHeaders").Get<string[]>() ?? new[] { "ETag" };

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("DefaultCorsPolicy", policy =>
                {
                    if (corsOrigins.Any())
                    {
                        policy.WithOrigins(corsOrigins);
                    }
                    else
                    {
                        // If no origins specified, allow any origin (not recommended for production)
                        policy.AllowAnyOrigin();
                    }

                    if (corsAllowedMethods.Contains("*"))
                    {
                        policy.AllowAnyMethod();
                    }
                    else
                    {
                        policy.WithMethods(corsAllowedMethods);
                    }

                    if (corsAllowedHeaders.Contains("*"))
                    {
                        policy.AllowAnyHeader();
                    }
                    else
                    {
                        policy.WithHeaders(corsAllowedHeaders);
                    }

                    if (corsExposedHeaders.Any())
                    {
                        policy.WithExposedHeaders(corsExposedHeaders);
                    }

                    if (corsAllowCredentials)
                    {
                        policy.AllowCredentials();
                    }
                });
            });

            // Configure rate limiting
            builder.Services.AddRateLimiter(options =>
            {
                // Default policy for all endpoints
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 100,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                // Policy for public/lookup endpoints (more permissive)
                options.AddPolicy("PublicApi", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Request.Headers.Host.ToString(),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 300,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                // Policy for authenticated endpoints (stricter)
                options.AddPolicy("AuthenticatedApi", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.Identity?.Name ?? "anonymous",
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 60,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                // Customize rejection response
                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    
                    TimeSpan? retryAfter = null;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue))
                    {
                        retryAfter = retryAfterValue;
                        context.HttpContext.Response.Headers[TenXEmpires.Server.Domain.Constants.StandardHeaders.RetryAfter] = ((int)retryAfterValue.TotalSeconds).ToString();
                    }

                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        code = "RATE_LIMIT_EXCEEDED",
                        message = "Too many requests. Please try again later.",
                        retryAfterSeconds = retryAfter?.TotalSeconds
                    }, cancellationToken: token);
                };
            });

            builder.Services.AddControllers();

            // Configure API versioning
            builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new HeaderApiVersionReader("X-Api-Version"),
                    new MediaTypeApiVersionReader("version")
                );
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                // Configure Swagger for each API version
                c.SwaggerDoc("v1", new()
                {
                    Title = "TenX Empires API",
                    Version = "v1",
                    Description = "REST API for TenX Empires - a turn-based strategy game (Version 1.0)",
                    Contact = new()
                    {
                        Name = "TenX Empires",
                        Url = new Uri("https://github.com/tenxempires")
                    }
                });

                // Enable XML comments for better API documentation
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }

                // Group endpoints by tags
                c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "Default" });
                c.DocInclusionPredicate((name, api) => true);

                // Enable example filters for rich API documentation
                c.ExampleFilters();
            });

            // Register example providers
            builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();

            // Register OpenAPI export services (for automatic YAML generation)
            builder.Services.AddSingleton<OpenApiExporter>();
            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddHostedService<OpenApiExportHostedService>();
            }

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // Enable CORS
            app.UseCors("DefaultCorsPolicy");

            app.UseAuthorization();

            // Enable RLS context (must be after UseAuthorization)
            app.UseRlsContext();

            // Enable rate limiting
            app.UseRateLimiter();

            app.MapControllers();

            app.MapFallbackToFile("/index.html");

            // Add Serilog request logging
            app.UseSerilogRequestLogging();

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
