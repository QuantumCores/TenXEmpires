using System;
using System.Collections.Generic;
using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.DataContracts;

/// <summary>
/// Manual save slot item from <see cref="Save"/>.
/// </summary>
public sealed record SaveManualDto(
    long Id,
    int Slot,
    int TurnNo,
    DateTimeOffset CreatedAt,
    string Name)
{
    public static SaveManualDto From(Save s) => new(s.Id, s.Slot ?? 0, s.TurnNo, s.CreatedAt, s.Name);
}

/// <summary>
/// Autosave item from <see cref="Save"/>.
/// </summary>
public sealed record SaveAutosaveDto(
    long Id,
    int TurnNo,
    DateTimeOffset CreatedAt)
{
    public static SaveAutosaveDto From(Save s) => new(s.Id, s.TurnNo, s.CreatedAt);
}

/// <summary>
/// Response for GET /games/{id}/saves
/// </summary>
public sealed record GameSavesListDto(
    IReadOnlyList<SaveManualDto> Manual,
    IReadOnlyList<SaveAutosaveDto> Autosaves);

/// <summary>
/// Response for POST /games/{id}/saves/manual
/// </summary>
public sealed record SaveCreatedDto(
    long Id,
    int Slot,
    int TurnNo,
    DateTimeOffset CreatedAt,
    string Name)
{
    public static SaveCreatedDto From(Save s) => new(s.Id, s.Slot ?? 0, s.TurnNo, s.CreatedAt, s.Name);
}

/// <summary>
/// Response for POST /saves/{saveId}/load
/// </summary>
public sealed record LoadSaveResponse(
    long GameId,
    GameStateDto State);

