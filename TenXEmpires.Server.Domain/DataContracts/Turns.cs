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

/// <summary>
/// Query parameters for listing game turns (GET /games/{id}/turns).
/// </summary>
public sealed class ListTurnsQuery
{
    /// <summary>
    /// 1-based page number (default: 1).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "Page must be >= 1")]
    public int Page { get; set; } = 1;
    
    /// <summary>
    /// Number of items per page (default: 20, max: 100).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
    public int PageSize { get; set; } = 20;
    
    /// <summary>
    /// Sort field: turnNo or committedAt (default: turnNo).
    /// </summary>
    public string? Sort { get; set; }
    
    /// <summary>
    /// Sort order: asc or desc (default: desc).
    /// </summary>
    public string? Order { get; set; }
}

