using System.Collections.Generic;
using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.DataContracts;

/// <summary>
/// DTO for <see cref="UnitDefinition"/> lookups.
/// </summary>
public sealed record UnitDefinitionDto(
    long Id,
    string Code,
    bool IsRanged,
    int Attack,
    int Defence,
    int RangeMin,
    int RangeMax,
    int MovePoints,
    int Health)
{
    public static UnitDefinitionDto From(UnitDefinition e) => new(
        e.Id,
        e.Code,
        e.IsRanged,
        e.Attack,
        e.Defence,
        e.RangeMin,
        e.RangeMax,
        e.MovePoints,
        e.Health);
}

/// <summary>
/// DTO for <see cref="Map"/> metadata.
/// </summary>
public sealed record MapDto(
    long Id,
    string Code,
    int SchemaVersion,
    int Width,
    int Height)
{
    public static MapDto From(Map e) => new(e.Id, e.Code, e.SchemaVersion, e.Width, e.Height);
}

/// <summary>
/// DTO for <see cref="MapTile"/> summaries.
/// </summary>
public sealed record MapTileDto(
    long Id,
    int Row,
    int Col,
    string Terrain,
    string? ResourceType,
    int ResourceAmount)
{
    public static MapTileDto From(MapTile e) => new(e.Id, e.Row, e.Col, e.Terrain, e.ResourceType, e.ResourceAmount);
}

