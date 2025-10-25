using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service responsible for ingesting client analytics events.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Ingests a batch of analytics events and returns the number of accepted (persisted) items.
    /// Performs normalization and pseudonymization (salted user key) per privacy policy.
    /// </summary>
    /// <param name="userId">Authenticated user id when available; otherwise null.</param>
    /// <param name="deviceId">Anonymous device/session id from cookie when available; otherwise null.</param>
    /// <param name="command">Batch of events submitted by the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of accepted events.</returns>
    Task<int> IngestBatchAsync(Guid? userId, string? deviceId, AnalyticsBatchCommand command, CancellationToken cancellationToken = default);
}

