using System;
using System.Text.Json;
using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.DataContracts;

/// <summary>
/// List item DTO for <see cref="Game"/>.
/// </summary>
public sealed record GameListItemDto(
    long Id,
    string Status,
    int TurnNo,
    long MapId,
    int MapSchemaVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? LastTurnAt)
{
    public static GameListItemDto From(Game g) => new(
        g.Id,
        g.Status,
        g.TurnNo,
        g.MapId,
        g.MapSchemaVersion,
        g.StartedAt,
        g.FinishedAt,
        g.LastTurnAt);
}

/// <summary>
/// Detail DTO for <see cref="Game"/>.
/// </summary>
public sealed record GameDetailDto(
    long Id,
    Guid UserId,
    long MapId,
    int MapSchemaVersion,
    int TurnNo,
    long? ActiveParticipantId,
    bool TurnInProgress,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? LastTurnAt,
    JsonDocument? Settings)
{
    public static GameDetailDto From(Game g)
    {
        JsonDocument? settings = null;
        if (!string.IsNullOrWhiteSpace(g.Settings))
        {
            try { settings = JsonDocument.Parse(g.Settings); }
            catch { settings = null; }
        }

        return new GameDetailDto(
            g.Id,
            g.UserId,
            g.MapId,
            g.MapSchemaVersion,
            g.TurnNo,
            g.ActiveParticipantId,
            g.TurnInProgress,
            g.Status,
            g.StartedAt,
            g.FinishedAt,
            g.LastTurnAt,
            settings);
    }
}

/// <summary>
/// Response shape for POST /games.
/// </summary>
public sealed record GameCreatedResponse(
    long Id,
    GameStateDto State);

/// <summary>
/// Query parameters for listing games (GET /games).
/// </summary>
public sealed class ListGamesQuery
{
    /// <summary>
    /// Filter by game status (active or finished).
    /// </summary>
    public string? Status { get; set; }
    
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
    /// Sort field: startedAt, lastTurnAt, or turnNo (default: lastTurnAt).
    /// </summary>
    public string? Sort { get; set; }
    
    /// <summary>
    /// Sort order: asc or desc (default: desc).
    /// </summary>
    public string? Order { get; set; }
}

