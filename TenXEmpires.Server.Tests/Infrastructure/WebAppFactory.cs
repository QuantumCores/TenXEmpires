using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Filters;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Tests.Infrastructure;

/// <summary>
/// Shared WebApplicationFactory for integration tests.
/// Replaces external dependencies with test-friendly implementations.
/// </summary>
public class WebAppFactory : WebApplicationFactory<TenXEmpires.Server.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            // Ensure MVC ViewFeatures (antiforgery filter services) are available
            services.AddControllersWithViews();
            // Replace Npgsql DbContext with in-memory for tests
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TenXDbContext>));
            if (dbDescriptor is not null)
            {
                services.Remove(dbDescriptor);
            }
            services.AddDbContext<TenXDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationTests"));

            // Replace antiforgery with a deterministic test implementation
            var anti = services.SingleOrDefault(d => d.ServiceType == typeof(IAntiforgery));
            if (anti is not null)
            {
                services.Remove(anti);
            }
            services.AddSingleton<IAntiforgery, TestAntiforgery>();

            // Replace analytics service to avoid persistence/config requirements
            var analyticsSvc = services.SingleOrDefault(d => d.ServiceType == typeof(IAnalyticsService));
            if (analyticsSvc is not null)
            {
                services.Remove(analyticsSvc);
            }
            services.AddSingleton<IAnalyticsService, TestAnalyticsService>();

            // Note: ValidateAntiforgeryTokenAuthorizationFilter is registered by AddControllersWithViews
        });
    }
}

internal sealed class TestAntiforgery : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
    {
        return new AntiforgeryTokenSet(
            requestToken: "integration-token",
            cookieToken: "integration-cookie",
            formFieldName: "__RequestVerificationToken",
            headerName: SecurityConstants.XsrfHeader);
    }

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext)
    {
        return GetAndStoreTokens(httpContext);
    }

    public Task<bool> IsRequestValidAsync(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers[SecurityConstants.XsrfHeader].ToString();
        return Task.FromResult(header == "integration-token");
    }

    public Task ValidateRequestAsync(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers[SecurityConstants.XsrfHeader].ToString();
        if (header != "integration-token")
        {
            throw new AntiforgeryValidationException("Invalid or missing CSRF token header.");
        }

        return Task.CompletedTask;
    }

    public void SetCookieTokenAndHeader(HttpContext httpContext)
    {
        httpContext.Response.Headers[SecurityConstants.XsrfHeader] = "integration-token";
    }
}

internal sealed class TestAnalyticsService : IAnalyticsService
{
    public Task<int> IngestBatchAsync(Guid? userId, string? deviceId, AnalyticsBatchCommand command, CancellationToken cancellationToken = default)
    {
        var count = command?.Events?.Count ?? 0;
        return Task.FromResult(count);
    }
}
