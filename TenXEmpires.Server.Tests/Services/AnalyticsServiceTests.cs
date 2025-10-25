using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

public class AnalyticsServiceTests
{
    private static TenXDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TenXDbContext(options);
    }

    [Fact]
    public async Task IngestBatch_ComputesUserKey_AndPersists()
    {
        await using var ctx = CreateContext(Guid.NewGuid().ToString());
        var logger = new Mock<ILogger<AnalyticsService>>().Object;
        var service = new AnalyticsService(ctx, logger);

        var salt = Encoding.UTF8.GetBytes("supersecret_salt");
        ctx.Settings.Add(new Setting { AnalyticsSalt = salt, SaltVersion = 3, UpdatedAt = DateTimeOffset.UtcNow });
        await ctx.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var cmd = new AnalyticsBatchCommand(new List<AnalyticsEventItem>
        {
            new("turn_end", 100, 7, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), null)
        });

        var accepted = await service.IngestBatchAsync(userId, null, cmd);
        accepted.Should().Be(1);

        var entity = await ctx.AnalyticsEvents.AsNoTracking().FirstAsync();
        entity.EventType.Should().Be("turn_end");
        entity.GameKey.Should().Be(100);
        entity.TurnNo.Should().Be(7);
        entity.SaltVersion.Should().Be(3);
        entity.UserKey.Should().HaveLength(64);

        // Verify HMAC format
        using var hmac = new HMACSHA256(salt);
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(userId.ToString("D")))).ToLowerInvariant();
        entity.UserKey.Should().Be(expected);
    }

    [Fact]
    public async Task IngestBatch_DedupesByClientRequestId()
    {
        await using var ctx = CreateContext(Guid.NewGuid().ToString());
        var logger = new Mock<ILogger<AnalyticsService>>().Object;
        var service = new AnalyticsService(ctx, logger);

        ctx.Settings.Add(new Setting { AnalyticsSalt = Encoding.UTF8.GetBytes("s"), SaltVersion = 1, UpdatedAt = DateTimeOffset.UtcNow });
        await ctx.SaveChangesAsync();

        var gid = Guid.NewGuid().ToString();
        var cmd = new AnalyticsBatchCommand(new List<AnalyticsEventItem>
        {
            new("turn_end", 100, 7, DateTimeOffset.UtcNow, gid, null),
            new("turn_end", 100, 7, DateTimeOffset.UtcNow, gid, null)
        });

        var accepted = await service.IngestBatchAsync(null, "device-1", cmd);
        accepted.Should().Be(1);
        (await ctx.AnalyticsEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task IngestBatch_NoSaltConfigured_ShouldThrow()
    {
        await using var ctx = CreateContext(Guid.NewGuid().ToString());
        var logger = new Mock<ILogger<AnalyticsService>>().Object;
        var service = new AnalyticsService(ctx, logger);

        var cmd = new AnalyticsBatchCommand(new List<AnalyticsEventItem>
        {
            new("turn_end", 100, 7, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), null)
        });

        var act = async () => await service.IngestBatchAsync(Guid.NewGuid(), null, cmd);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

