using System;
using System.Collections.Generic;
using System.Text.Json;
using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.DataContracts;

/// <summary>
/// Aggregate state projection for a game. Built from <see cref="Game"/> and its children.
/// </summary>
public sealed record GameStateDto(
    GameStateGameDto Game,
    GameStateMapDto Map,
    IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyList<UnitInStateDto> Units,
    IReadOnlyList<CityInStateDto> Cities,
    IReadOnlyList<CityTileLinkDto> CityTiles,
    IReadOnlyList<CityResourceDto> CityResources,
    IReadOnlyList<GameTileStateDto>? GameTiles,
    IReadOnlyList<UnitDefinitionDto> UnitDefinitions,
    JsonDocument? TurnSummary)
{
}

/// <summary>
/// Subset of <see cref="Entities.Game"/> published in state.
/// </summary>
public sealed record GameStateGameDto(
    long Id,
    int TurnNo,
    long? ActiveParticipantId,
    bool TurnInProgress,
    string Status)
{
    public static GameStateGameDto From(Game g) => new(g.Id, g.TurnNo, g.ActiveParticipantId, g.TurnInProgress, g.Status);
}

/// <summary>
/// Subset of <see cref="Entities.Map"/> published in state.
/// </summary>
public sealed record GameStateMapDto(
    long Id,
    string Code,
    int SchemaVersion,
    int Width,
    int Height)
{
    public static GameStateMapDto From(Map m) => new(m.Id, m.Code, m.SchemaVersion, m.Width, m.Height);
}

/// <summary>
/// Participant summary DTO from <see cref="Entities.Participant"/>.
/// </summary>
public sealed record ParticipantDto(
    long Id,
    long GameId,
    string Kind,
    Guid? UserId,
    string DisplayName,
    bool IsEliminated)
{
    public static ParticipantDto From(Participant p) => new(p.Id, p.GameId, p.Kind, p.UserId, p.DisplayName, p.IsEliminated);
}

/// <summary>
/// Unit view in state; combines <see cref="Entities.Unit"/>, <see cref="Entities.UnitDefinition"/> and <see cref="Entities.MapTile"/>.
/// </summary>
public sealed record UnitInStateDto(
    long Id,
    long ParticipantId,
    string TypeCode,
    int Hp,
    bool HasActed,
    long TileId,
    int Row,
    int Col)
{
    public static UnitInStateDto From(Unit u)
        => new(u.Id, u.ParticipantId, u.Type.Code, u.Hp, u.HasActed, u.TileId, u.Tile.Row, u.Tile.Col);
}

/// <summary>
/// City view in state; combines <see cref="Entities.City"/> and <see cref="Entities.MapTile"/>.
/// </summary>
public sealed record CityInStateDto(
    long Id,
    long ParticipantId,
    int Hp,
    int MaxHp,
    long TileId,
    int Row,
    int Col)
{
    public static CityInStateDto From(City c)
        => new(c.Id, c.ParticipantId, c.Hp, c.MaxHp, c.TileId, c.Tile.Row, c.Tile.Col);
}

/// <summary>
/// Link between city and tile; from <see cref="Entities.CityTile"/>.
/// </summary>
public sealed record CityTileLinkDto(
    long CityId,
    long TileId)
{
    public static CityTileLinkDto From(CityTile ct) => new(ct.CityId, ct.TileId);
}

/// <summary>
/// City resource view; from <see cref="Entities.CityResource"/>.
/// </summary>
public sealed record CityResourceDto(
    long CityId,
    string ResourceType,
    int Amount)
{
    public static CityResourceDto From(CityResource cr) => new(cr.CityId, cr.ResourceType, cr.Amount);
}

/// <summary>
/// Per-game mutable tile state (e.g., remaining resources).
/// </summary>
public sealed record GameTileStateDto(
    long TileId,
    string? ResourceType,
    int ResourceAmount)
{
    public static GameTileStateDto From(GameTileState state)
        => new(state.TileId, state.Tile.ResourceType, state.ResourceAmount);
}

/// <summary>
/// Response shape for action endpoints that return only updated state.
/// </summary>
public sealed record ActionStateResponse(GameStateDto State);

/// <summary>
/// Response for end-turn: includes state, turn summary and created autosave ID.
/// </summary>
public sealed record EndTurnResponse(GameStateDto State, JsonDocument TurnSummary, long AutosaveId);
