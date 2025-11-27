using System;
using System.Collections.Generic;
using System.Text.Json;
using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.DataContracts;

/// <summary>
/// Command to create a new <see cref="Game"/>.
/// </summary>
public sealed record CreateGameCommand(
    string? MapCode,
    JsonDocument? Settings,
    string? DisplayName);

/// <summary>
/// Command to move a <see cref="Unit"/> to a target grid position.
/// </summary>
public sealed record MoveUnitCommand(long UnitId, GridPosition To);

/// <summary>
/// Command to attack with one <see cref="Unit"/> another <see cref="Unit"/>.
/// </summary>
public sealed record AttackUnitCommand(long AttackerUnitId, long TargetUnitId);

/// <summary>
/// Command to attack a <see cref="City"/> with a <see cref="Unit"/>.
/// </summary>
public sealed record AttackCityCommand(long AttackerUnitId, long TargetCityId);

/// <summary>
/// Command to spawn a unit from a city using stored resources.
/// </summary>
public sealed record SpawnUnitCommand(long CityId, string UnitCode);

/// <summary>
/// Command to end the active participant's turn in a <see cref="Game"/>.
/// </summary>
public sealed record EndTurnCommand;

/// <summary>
/// Command to create/overwrite a manual <see cref="Save"/> in a slot.
/// </summary>
public sealed record CreateManualSaveCommand(int Slot, string Name);

/// <summary>
/// Command to load a <see cref="Save"/> into its <see cref="Game"/>.
/// </summary>
public sealed record LoadSaveCommand;

/// <summary>
/// Position on the map grid.
/// </summary>
public sealed record GridPosition(int Row, int Col);

/// <summary>
/// Batch analytics command (maps to <see cref="AnalyticsEvent"/> rows on persistence).
/// </summary>
public sealed record AnalyticsBatchCommand(IReadOnlyList<AnalyticsEventItem> Events);

public sealed record AnalyticsEventItem(
    string EventType,
    long? GameId,
    int? TurnNo,
    DateTimeOffset? OccurredAt,
    string? ClientRequestId,
    JsonDocument? Payload);
