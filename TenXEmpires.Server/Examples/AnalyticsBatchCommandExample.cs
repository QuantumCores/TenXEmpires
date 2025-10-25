using Swashbuckle.AspNetCore.Filters;
using System.Text.Json;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

public sealed class AnalyticsBatchCommandExample : IExamplesProvider<AnalyticsBatchCommand>
{
    public AnalyticsBatchCommand GetExamples()
    {
        var payload = JsonDocument.Parse("{\n  \"fps\": 58,\n  \"latencyMs\": 120\n}");
        return new AnalyticsBatchCommand(new List<AnalyticsEventItem>
        {
            new(
                EventType: "turn_end",
                GameId: 42,
                TurnNo: 5,
                OccurredAt: DateTimeOffset.UtcNow,
                ClientRequestId: Guid.NewGuid().ToString(),
                Payload: payload),
            new(
                EventType: "autosave",
                GameId: 42,
                TurnNo: 5,
                OccurredAt: DateTimeOffset.UtcNow,
                ClientRequestId: Guid.NewGuid().ToString(),
                Payload: null)
        });
    }
}

