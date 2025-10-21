using System;
using System.Text.Json;
using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.DataContracts;

/// <summary>
/// DTO for committed turns from <see cref="Turn"/>.
/// </summary>
public sealed record TurnDto(
    long Id,
    int TurnNo,
    long ParticipantId,
    DateTimeOffset CommittedAt,
    int? DurationMs,
    JsonDocument? Summary)
{
    public static TurnDto From(Turn t)
    {
        JsonDocument? summary = null;
        if (!string.IsNullOrWhiteSpace(t.Summary))
        {
            try { summary = JsonDocument.Parse(t.Summary); }
            catch { summary = null; }
        }
        return new TurnDto(t.Id, t.TurnNo, t.ParticipantId, t.CommittedAt, t.DurationMs, summary);
    }
}

