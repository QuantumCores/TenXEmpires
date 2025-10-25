using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="IAnalyticsService"/> that persists analytics events
/// using EF Core and applies privacy-preserving pseudonymization.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly TenXDbContext _db;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(TenXDbContext db, ILogger<AnalyticsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> IngestBatchAsync(Guid? userId, string? deviceId, AnalyticsBatchCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (command.Events is null) throw new ArgumentException("Events collection is required.");

        // Load settings for analytics salt (latest)
        var setting = await _db.Settings
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (setting is null || setting.AnalyticsSalt is null || setting.AnalyticsSalt.Length == 0)
        {
            _logger.LogError("Analytics salt not configured in settings.");
            throw new InvalidOperationException("Analytics is not configured.");
        }

        var (userKey, saltVersion) = ComputeUserKey(setting, userId, deviceId);

        var accepted = 0;

        foreach (var item in command.Events)
        {
            // Basic validation per-item
            if (string.IsNullOrWhiteSpace(item.EventType))
            {
                _logger.LogWarning("Skipping analytics item with empty eventType");
                continue;
            }

            // Normalize timestamp
            var occurredAt = item.OccurredAt?.ToUniversalTime() ?? DateTimeOffset.UtcNow;

            // Parse client request id if provided
            Guid? clientRequestId = null;
            if (!string.IsNullOrWhiteSpace(item.ClientRequestId))
            {
                if (Guid.TryParse(item.ClientRequestId, out var parsed))
                {
                    clientRequestId = parsed;
                }
                else
                {
                    _logger.LogWarning("Skipping item with invalid clientRequestId: {ClientRequestId}", item.ClientRequestId);
                    continue;
                }
            }

            // Optional pre-check for idempotency to reduce duplicates before DB unique index
            if (clientRequestId.HasValue)
            {
                var exists = await _db.AnalyticsEvents.AnyAsync(e => e.ClientRequestId == clientRequestId, cancellationToken);
                if (exists)
                {
                    _logger.LogDebug("Analytics event with client_request_id already exists; skipping.");
                    continue;
                }
            }

            var entity = new AnalyticsEvent
            {
                EventType = item.EventType,
                OccurredAt = occurredAt,
                GameKey = item.GameId ?? 0,
                TurnNo = item.TurnNo,
                UserKey = userKey,
                SaltVersion = saltVersion,
                ClientRequestId = clientRequestId
            };

            _db.AnalyticsEvents.Add(entity);

            try
            {
                // Save one-by-one to gracefully handle duplicates via unique index on client_request_id
                accepted += await _db.SaveChangesAsync(cancellationToken) > 0 ? 1 : 0;
            }
            catch (DbUpdateException dbEx) when (dbEx.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // Duplicate client_request_id - treat as not accepted, move on (idempotent)
                _db.Entry(entity).State = EntityState.Detached;
                _logger.LogDebug("Duplicate analytics client_request_id detected; skipping insert.");
            }
        }

        return accepted;
    }

    private static (string userKey, int saltVersion) ComputeUserKey(Setting setting, Guid? userId, string? deviceId)
    {
        // Prefer authenticated user id; otherwise anonymous device id; fallback to string "anonymous"
        var identity = userId?.ToString("D") ?? (!string.IsNullOrWhiteSpace(deviceId) ? deviceId!.Trim() : "anonymous");

        // HMACSHA256(salt, identity) -> 64-char lowercase hex
        using var hmac = new HMACSHA256(setting.AnalyticsSalt);
        var bytes = Encoding.UTF8.GetBytes(identity);
        var hash = hmac.ComputeHash(bytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return (hex, setting.SaltVersion);
    }
}
