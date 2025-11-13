using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
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
    public async Task Post_AnalyticsBatch_ShouldReturn202()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

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

        // Send analytics batch (CSRF validation removed)
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/analytics/batch");
        request.Content = content;

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted, $"body: {body}");
    }

    [Fact]
    public async Task Get_KeepAlive_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/v1/auth/keepalive");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_KeepAlive_Authenticated_Returns204_AndNoStore()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/keepalive");
        request.Headers.Add("X-Test-Auth", "1");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Get_KeepAlive_Authenticated_ReissuesAuthCookie()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/keepalive");
        // Opt-in to test authentication; server will issue cookie on keepalive
        request.Headers.Add("X-Test-Auth", "1");
        request.Headers.Add("X-Test-User", "cookie-actor");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.TryGetValues("Set-Cookie", out var setCookieValues).Should().BeTrue();
        var setCookie = string.Join("; ", setCookieValues!);
        setCookie.Should().Contain("tenx.auth=");
        setCookie.Should().ContainEquivalentOf("SameSite=Lax");
        // Note: Auth cookie Secure flag is controlled by ASP.NET Core Identity configuration
        // In development/test environment, it should follow the same pattern as CSRF cookies
    }

    [Fact]
    public async Task Get_KeepAlive_RateLimited_Returns429_WithRetryAfter_AndPayload()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        HttpResponseMessage? last = null;
        var found429 = false;
        for (var i = 0; i < 65; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/v1/auth/keepalive");
            req.Headers.Add("X-Test-Auth", "1");
            req.Headers.Add("X-Test-User", "rate-limit-actor");
            last = await client.SendAsync(req);
            if (last.StatusCode == HttpStatusCode.TooManyRequests)
            {
                found429 = true;
                break;
            }
        }

        found429.Should().BeTrue("rate limit should trigger within 65 requests");

        // Validate headers and payload
        last!.Headers.TryGetValues("Retry-After", out var retryHeaders).Should().BeTrue();
        var retryAfter = retryHeaders!.FirstOrDefault();
        retryAfter.Should().NotBeNull();

        var body = await last.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.GetProperty("code").GetString().Should().Be("RATE_LIMIT_EXCEEDED");
        root.TryGetProperty("retryAfterSeconds", out var retryProp).Should().BeTrue();
        retryProp.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Number);
    }
}
