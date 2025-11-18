using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Domain.DataContracts;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Service for building game state projections.
/// </summary>
public class GameStateService : IGameStateService
{
    private readonly TenXDbContext _context;
    private readonly ILogger<GameStateService> _logger;

    public GameStateService(TenXDbContext context, ILogger<GameStateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<GameStateDto> BuildGameStateAsync(
        long gameId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Building game state for game {GameId}", gameId);

        // Load game with map
        var game = await _context.Games
            .AsNoTracking()
            .Include(g => g.Map)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game == null)
        {
            throw new InvalidOperationException($"Game with ID {gameId} not found.");
        }

        // Load participants
        var participants = await _context.Participants
            .AsNoTracking()
            .Where(p => p.GameId == gameId)
            .OrderBy(p => p.Id)
            .Select(p => ParticipantDto.From(p))
            .ToListAsync(cancellationToken);

        // Load units with their type and tile information
        var units = await _context.Units
            .AsNoTracking()
            .Include(u => u.Type)
            .Include(u => u.Tile)
            .Where(u => u.GameId == gameId)
            .OrderBy(u => u.Id)
            .Select(u => UnitInStateDto.From(u))
            .ToListAsync(cancellationToken);

        // Load cities with tile information
        var cities = await _context.Cities
            .AsNoTracking()
            .Include(c => c.Tile)
            .Where(c => c.GameId == gameId)
            .OrderBy(c => c.Id)
            .Select(c => CityInStateDto.From(c))
            .ToListAsync(cancellationToken);

        // Load city tiles
        var cityTiles = await _context.CityTiles
            .AsNoTracking()
            .Where(ct => ct.GameId == gameId)
            .OrderBy(ct => ct.CityId)
            .ThenBy(ct => ct.TileId)
            .Select(ct => CityTileLinkDto.From(ct))
            .ToListAsync(cancellationToken);

        // Load city resources
        var cityResources = await _context.CityResources
            .AsNoTracking()
            .Where(cr => cities.Select(c => c.Id).Contains(cr.CityId))
            .OrderBy(cr => cr.CityId)
            .ThenBy(cr => cr.ResourceType)
            .Select(cr => CityResourceDto.From(cr))
            .ToListAsync(cancellationToken);

        // Load per-game tile states (mutable resource amounts)
        var gameTileStates = await _context.GameTileStates
            .AsNoTracking()
            .Include(ts => ts.Tile)
            .Where(ts => ts.GameId == gameId)
            .OrderBy(ts => ts.TileId)
            .Select(ts => GameTileStateDto.From(ts))
            .ToListAsync(cancellationToken);

        // Load all unit definitions (for client reference)
        var unitDefinitions = await _context.UnitDefinitions
            .AsNoTracking()
            .OrderBy(ud => ud.Code)
            .Select(ud => UnitDefinitionDto.From(ud))
            .ToListAsync(cancellationToken);

        // Build game state DTO
        var gameStateDto = new GameStateDto(
            GameStateGameDto.From(game),
            GameStateMapDto.From(game.Map),
            participants,
            units,
            cities,
            cityTiles,
            cityResources,
            gameTileStates,
            unitDefinitions,
            TurnSummary: null); // No turn summary for initial state

        _logger.LogInformation(
            "Built game state for game {GameId}: {ParticipantCount} participants, {UnitCount} units, {CityCount} cities",
            gameId,
            participants.Count,
            units.Count,
            cities.Count);

        return gameStateDto;
    }
}

