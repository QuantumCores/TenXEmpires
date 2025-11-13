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
using Microsoft.AspNetCore.Authentication.Cookies;
using TenXEmpires.Server.Domain.DataContracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Diagnostics;

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
                options.UseNpgsql(connectionString)
                       .EnableSensitiveDataLogging()
                       .EnableDetailedErrors());
            builder.Services.AddDbContext<AppIdentityDbContext>(options =>
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
            builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
            builder.Services.AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>();
            builder.Services.AddSingleton<IAiNameGenerator, AiNameGenerator>();

            // Add memory cache for caching lookup data
            builder.Services.AddMemoryCache();

            // Add response caching for HTTP caching support
            builder.Services.AddResponseCaching();

            // Configure forwarded headers for proxy/load balancer support (DigitalOcean App Platform)
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
                // Trust all proxies (DigitalOcean App Platform)
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // Configure shared cookie domain for cross-subdomain SPA
            var sharedCookieDomain = builder.Configuration["Cookies:SharedDomain"];

            // Configure Antiforgery for SPA CSRF protection
            builder.Services.AddAntiforgery(o =>
            {
                o.HeaderName = TenXEmpires.Server.Domain.Constants.SecurityConstants.XsrfHeader;
                o.Cookie.SameSite = builder.Environment.IsDevelopment()
                    ? SameSiteMode.Lax
                    : SameSiteMode.None;
                o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
                    ? CookieSecurePolicy.None 
                    : CookieSecurePolicy.Always;
                o.Cookie.Domain = sharedCookieDomain;
                o.Cookie.Path = "/";
                o.SuppressXFrameOptionsHeader = false;
            });

            // Configure CORS
            var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            var corsAllowCredentials = builder.Configuration.GetValue<bool>("Cors:AllowCredentials", true);
            var corsAllowedMethods = builder.Configuration.GetSection("Cors:AllowedMethods").Get<string[]>() ?? new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
            var corsAllowedHeaders = builder.Configuration.GetSection("Cors:AllowedHeaders").Get<string[]>() ?? new[] { "*" };
            var defaultExposedHeaders = new[]
            {
                "ETag",
                "X-Tenx-Total-Count",
                TenXEmpires.Server.Domain.Constants.SecurityConstants.XsrfHeader
            };
            var corsExposedHeaders = builder.Configuration.GetSection("Cors:ExposedHeaders").Get<string[]>() ?? defaultExposedHeaders;

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

                // Policy for analytics ingestion (per-identity: userId, else device cookie, else IP)
                options.AddPolicy("AnalyticsIngest", httpContext =>
                {
                    string key;
                    if (httpContext.User?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(httpContext.User.Identity?.Name))
                    {
                        key = httpContext.User.Identity!.Name!;
                    }
                    else if (httpContext.Request.Cookies.TryGetValue("tenx.sid", out var device))
                    {
                        key = $"device:{device}";
                    }
                    else
                    {
                        key = $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString()}";
                    }

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: key,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 60,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        });
                });

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

            builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme, options =>
                {
                    options.Cookie.Name = "tenx.auth";
                    options.Cookie.SameSite = builder.Environment.IsDevelopment()
                        ? SameSiteMode.Lax
                        : SameSiteMode.None;
                    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
                        ? CookieSecurePolicy.None 
                        : CookieSecurePolicy.Always;
                    options.Cookie.Domain = sharedCookieDomain;
                    options.Cookie.HttpOnly = true;
                    options.SlidingExpiration = true;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnRedirectToLogin = ctx =>
                        {
                            // Return 401 JSON for APIs instead of redirecting
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return ctx.Response.WriteAsJsonAsync(new ApiErrorDto("UNAUTHORIZED", "User must be authenticated."));
                        },
                        OnRedirectToAccessDenied = ctx =>
                        {
                            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return ctx.Response.WriteAsJsonAsync(new ApiErrorDto("FORBIDDEN", "Access is denied."));
                        }
                    };
                });

            builder.Services.AddAuthorization();

            // ASP.NET Core Identity (EF stores in 'auth' schema)
            builder.Services
                .AddIdentityCore<IdentityUser<Guid>>(options =>
                {
                    options.Password.RequireDigit = true;
                    options.Password.RequiredLength = 8;
                    options.Password.RequireNonAlphanumeric = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireLowercase = true;
                    options.User.RequireUniqueEmail = true;
                    
                    // Disable email confirmation requirement in development
                    options.SignIn.RequireConfirmedEmail = !builder.Environment.IsDevelopment();
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<AppIdentityDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

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

            // Register database migration service
            builder.Services.AddSingleton<Infrastructure.DatabaseMigrationService>(sp => 
                new Infrastructure.DatabaseMigrationService(sp.GetRequiredService<IConfiguration>()));

            var app = builder.Build();

            // Run database migrations on startup (only in production, skip in development for faster startup)
            if (!app.Environment.IsDevelopment())
            {
                try
                {
                    var migrationService = app.Services.GetRequiredService<Infrastructure.DatabaseMigrationService>();
                    Log.Information("Running database migrations on startup...");
                    
                    var migrationSuccess = migrationService.RunMigrations(ensureDatabase: false);
                    if (!migrationSuccess)
                    {
                        Log.Fatal("Database migrations failed. Application will not start.");
                        throw new InvalidOperationException("Database migrations failed. Check logs for details.");
                    }
                    
                    Log.Information("Database migrations completed successfully.");
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Failed to run database migrations on startup");
                    throw;
                }
            }

            // Use forwarded headers FIRST (before any other middleware that needs to know about the original request)
            app.UseForwardedHeaders();

            // Global exception handler for API endpoints (returns JSON instead of HTML)
            // This ensures all unhandled exceptions return consistent JSON error responses
            app.UseExceptionHandler(appError =>
            {
                appError.Run(async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json";

                    var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                    if (contextFeature != null)
                    {
                        var exception = contextFeature.Error;
                        Log.Error(exception, "Unhandled exception: {Message}", exception.Message);
                        
                        var errorResponse = new ApiErrorDto(
                            "INTERNAL_ERROR",
                            app.Environment.IsDevelopment() 
                                ? exception.Message 
                                : "An error occurred while processing your request."
                        );
                        
                        await context.Response.WriteAsJsonAsync(errorResponse);
                    }
                });
            });

            // Only serve static files if in development (for local testing)
            if (app.Environment.IsDevelopment())
            {
                app.UseDefaultFiles();
                app.UseStaticFiles();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // HTTPS redirection - disabled in production since we're behind a proxy that handles SSL termination
            // Forwarded headers middleware above will make the app aware of the original HTTPS protocol
            if (app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            // Enable response caching (must be before CORS and auth)
            app.UseResponseCaching();

            // Enable CORS
            app.UseCors("DefaultCorsPolicy");

            // Enable authn/z
            app.UseAuthentication();
            app.UseAuthorization();

            // Enable RLS context (must be after UseAuthorization)
            app.UseRlsContext();

            // Enable rate limiting
            app.UseRateLimiter();

            app.MapControllers();

            // Only serve SPA fallback if in development (for local testing)
            if (app.Environment.IsDevelopment())
            {
                app.MapFallbackToFile("/index.html");
            }

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
