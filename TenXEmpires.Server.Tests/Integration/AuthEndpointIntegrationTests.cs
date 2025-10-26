using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using System.Text.Json;
using TenXEmpires.Server.Tests.Infrastructure;

namespace TenXEmpires.Server.Tests.Integration;

public class AuthEndpointIntegrationTests : IClassFixture<WebAppFactory>
{
    private readonly WebAppFactory _factory;

    public AuthEndpointIntegrationTests(WebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Csrf_IssuesXsrfCookie_WithSecureLax()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/v1/auth/csrf");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        response.Headers.TryGetValues("Set-Cookie", out var setCookieValues).Should().BeTrue();
        var setCookie = string.Join("; ", setCookieValues!);
        setCookie.Should().Contain("XSRF-TOKEN=");
        setCookie.Should().ContainEquivalentOf("Secure");
        setCookie.Should().ContainEquivalentOf("SameSite=Lax");
        setCookie.Should().ContainEquivalentOf("Path=/");

        response.Headers.CacheControl.NoStore.Should().BeTrue();
    }
    [Fact]
    public async Task Post_AnalyticsBatch_WithXsrfHeader_ShouldReturn202()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Obtain token cookie (not strictly required by test antiforgery but mirrors client flow)
        var csrf = await client.GetAsync("/v1/auth/csrf");
        csrf.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        // Prepare minimal valid analytics batch
        var payload = new
        {
            events = new[]
            {
                new { eventType = "custom.test", gameId = (long?)null, turnNo = (int?)null, occurredAt = (DateTimeOffset?)null, clientRequestId = (string?)null, payload = (object?)null }
            }
        };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Send with XSRF header expected by our test antiforgery
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/analytics/batch");
        request.Content = content;
        request.Headers.Add("X-XSRF-TOKEN", "integration-token");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted, $"body: {body}");
    }
}
