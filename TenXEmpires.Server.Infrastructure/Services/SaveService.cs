using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Service for creating and managing game saves.
/// </summary>
public class SaveService : ISaveService
{
    private const int AutosaveCapacity = 5;

    private readonly TenXDbContext _context;
    private readonly ILogger<SaveService> _logger;

    public SaveService(TenXDbContext context, ILogger<SaveService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<long> CreateAutosaveAsync(
        Guid userId,
        long gameId,
        int turnNo,
        long activeParticipantId,
        int schemaVersion,
        string mapCode,
        GameStateDto state,
        CancellationToken cancellationToken = default)
    {
        // Serialize state to JSON (store as-is; compression handled by storage engine if configured)
        var stateJson = JsonSerializer.Serialize(state);

        var save = new Save
        {
            UserId = userId,
            GameId = gameId,
            Kind = "autosave",
            Name = $"Autosave - Turn {turnNo}",
            Slot = null,
            TurnNo = turnNo,
            ActiveParticipantId = activeParticipantId,
            SchemaVersion = schemaVersion,
            MapCode = mapCode,
            State = stateJson,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Saves.Add(save);
        await _context.SaveChangesAsync(cancellationToken);

        // Enforce ring buffer: keep most recent AutosaveCapacity autosaves
        var autosaves = await _context.Saves
            .Where(s => s.GameId == gameId && s.Kind == "autosave")
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Id, s.CreatedAt })
            .ToListAsync(cancellationToken);

        if (autosaves.Count > AutosaveCapacity)
        {
            var toDeleteIds = autosaves
                .Skip(AutosaveCapacity)
                .Select(s => s.Id)
                .ToArray();

            int deleted;
            try
            {
                deleted = await _context.Saves
                    .Where(s => toDeleteIds.Contains(s.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }
            catch (NotSupportedException)
            {
                var stale = await _context.Saves.Where(s => toDeleteIds.Contains(s.Id)).ToListAsync(cancellationToken);
                _context.Saves.RemoveRange(stale);
                await _context.SaveChangesAsync(cancellationToken);
                deleted = stale.Count;
            }
            catch (InvalidOperationException)
            {
                var stale = await _context.Saves.Where(s => toDeleteIds.Contains(s.Id)).ToListAsync(cancellationToken);
                _context.Saves.RemoveRange(stale);
                await _context.SaveChangesAsync(cancellationToken);
                deleted = stale.Count;
            }

            _logger.LogInformation(
                "Autosave ring buffer enforced for game {GameId}: kept {Kept}, deleted {Deleted}",
                gameId,
                AutosaveCapacity,
                deleted);
        }

        return save.Id;
    }
}
