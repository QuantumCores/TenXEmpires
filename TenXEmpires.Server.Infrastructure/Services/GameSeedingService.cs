using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Services;
using TenXEmpires.Server.Domain.Utilities;
using TenXEmpires.Server.Infrastructure.Data;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Service for seeding initial game entities in a deterministic manner.
/// </summary>
public class GameSeedingService : IGameSeedingService
{
    private readonly TenXDbContext _context;
    private readonly ILogger<GameSeedingService> _logger;

    // Starting configuration constants
    private const int InitialCityHp = 100;
    private const int InitialCityMaxHp = 100;

    public GameSeedingService(TenXDbContext context, ILogger<GameSeedingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedGameEntitiesAsync(
        long gameId,
        long mapId,
        long humanParticipantId,
        long aiParticipantId,
        long rngSeed,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Seeding entities for game {GameId} on map {MapId}",
            gameId,
            mapId);

        // Get unit definitions (warrior only for MVP)
        var unitDefinitions = await _context.UnitDefinitions
            .AsNoTracking()
            .Where(u => u.Code == UnitTypes.Warrior)
            .ToDictionaryAsync(u => u.Code, u => u, cancellationToken);

        if (!unitDefinitions.ContainsKey(UnitTypes.Warrior))
        {
            throw new InvalidOperationException($"Unit definition '{UnitTypes.Warrior}' not found in database.");
        }

        // Get map dimensions and tiles
        var map = await _context.Maps
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mapId, cancellationToken);

        if (map == null)
        {
            throw new InvalidOperationException($"Map with ID {mapId} not found.");
        }

        var allTiles = await _context.MapTiles
            .AsNoTracking()
            .Where(t => t.MapId == mapId)
            .OrderBy(t => t.Row)
            .ThenBy(t => t.Col)
            .ToListAsync(cancellationToken);

        if (allTiles.Count == 0)
        {
            throw new InvalidOperationException($"No tiles found for map {mapId}.");
        }

        // Create per-game tile state (copy template resource amounts)
        var tileStates = allTiles.Select(t => new GameTileState
        {
            GameId = gameId,
            TileId = t.Id,
            ResourceAmount = t.ResourceAmount
        }).ToList();
        _context.GameTileStates.AddRange(tileStates);
        await _context.SaveChangesAsync(cancellationToken);

        // Determine starting positions using RNG seed
        var random = new Random((int)(rngSeed % int.MaxValue));
        //var startingPositions = DetermineStartingPositions(allTiles, map.Width, map.Height, random);


        //if (startingPositions.Count < 2)
        //{
        //    throw new InvalidOperationException(
        //        $"Not enough valid starting positions found on map. Found {startingPositions.Count}, need at least 2.");
        //}

        var humanStartTile = allTiles.Single(x => x.Row == 4 && x.Col == 2);
        var aiStartTile = allTiles.Single(x => x.Row == 12 && x.Col == 17);

        // Create starting cities
        var humanCity = new City
        {
            GameId = gameId,
            ParticipantId = humanParticipantId,
            TileId = humanStartTile.Id,
            Hp = InitialCityHp,
            MaxHp = InitialCityMaxHp,
            HasActedThisTurn = false
        };

        var aiCity = new City
        {
            GameId = gameId,
            ParticipantId = aiParticipantId,
            TileId = aiStartTile.Id,
            Hp = InitialCityHp,
            MaxHp = InitialCityMaxHp,
            HasActedThisTurn = false
        };

        _context.Cities.AddRange(humanCity, aiCity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created starting cities: human={HumanCityId} at ({HumanRow},{HumanCol}), ai={AiCityId} at ({AiRow},{AiCol})",
            humanCity.Id,
            humanStartTile.Row,
            humanStartTile.Col,
            aiCity.Id,
            aiStartTile.Row,
            aiStartTile.Col);

        // Create city tiles (initially just the city's own tile)
        var humanCityTiles = new List<CityTile>()
        {
            new CityTile{
                GameId = gameId,
                CityId = humanCity.Id,
                TileId = humanStartTile.Id
            }
        };
        var humanManagedTiles = allTiles
            .Where(x =>
                x.Row == 3 && x.Col == 1 ||
                x.Row == 3 && x.Col == 2 ||
                x.Row == 4 && x.Col == 1 ||
                x.Row == 4 && x.Col == 3 ||
                x.Row == 5 && x.Col == 2 ||
                x.Row == 6 && x.Col == 1
            )
            .Select(x =>
                new CityTile {
                    GameId = gameId,
                    CityId = humanCity.Id,
                    TileId = x.Id,
                })
            .ToList();
        humanCityTiles.AddRange(humanManagedTiles);

        var aiCityTiles = new List<CityTile>()
        {
            new CityTile{
                GameId = gameId,
                CityId = aiCity.Id,
                TileId = aiStartTile.Id
            }
        };
        var aiManagedTiles = allTiles
            .Where(x =>
                x.Row == 12 && x.Col == 18 ||
                x.Row == 13 && x.Col == 16 ||
                x.Row == 12 && x.Col == 16 ||
                x.Row == 13 && x.Col == 17 ||
                x.Row == 11 && x.Col == 16 ||
                x.Row == 10 && x.Col == 18
            )
            .Select(x =>
                new CityTile
                {
                    GameId = gameId,
                    CityId = aiCity.Id,
                    TileId = x.Id,
                })
            .ToList();
        aiCityTiles.AddRange(aiManagedTiles);

        _context.CityTiles.AddRange(humanCityTiles);
        _context.CityTiles.AddRange(aiCityTiles);
        await _context.SaveChangesAsync(cancellationToken);

        // Initialize city resources with proper resource types
        var humanCityResources = new[]
        {
            new CityResource { CityId = humanCity.Id, ResourceType = ResourceTypes.Wood, Amount = ResourceTypes.InitialAmounts.Wood },
            new CityResource { CityId = humanCity.Id, ResourceType = ResourceTypes.Stone, Amount = ResourceTypes.InitialAmounts.Stone },
            new CityResource { CityId = humanCity.Id, ResourceType = ResourceTypes.Wheat, Amount = ResourceTypes.InitialAmounts.Wheat },
            new CityResource { CityId = humanCity.Id, ResourceType = ResourceTypes.Iron, Amount = ResourceTypes.InitialAmounts.Iron }
        };

        var aiCityResources = new[]
        {
            new CityResource { CityId = aiCity.Id, ResourceType = ResourceTypes.Wood, Amount = ResourceTypes.InitialAmounts.Wood },
            new CityResource { CityId = aiCity.Id, ResourceType = ResourceTypes.Stone, Amount = ResourceTypes.InitialAmounts.Stone },
            new CityResource { CityId = aiCity.Id, ResourceType = ResourceTypes.Wheat, Amount = ResourceTypes.InitialAmounts.Wheat },
            new CityResource { CityId = aiCity.Id, ResourceType = ResourceTypes.Iron, Amount = ResourceTypes.InitialAmounts.Iron }
        };

        _context.CityResources.AddRange(humanCityResources);
        _context.CityResources.AddRange(aiCityResources);
        await _context.SaveChangesAsync(cancellationToken);

        // Create starting units (warrior for each side)
        var warriorDef = unitDefinitions[UnitTypes.Warrior];

        // Find adjacent tiles for unit placement using HexagonalGrid utility
        var humanUnitTile = HexagonalGrid.FindAdjacentTile(humanStartTile, allTiles, map.Width, map.Height, random);
        var aiUnitTile = HexagonalGrid.FindAdjacentTile(aiStartTile, allTiles, map.Width, map.Height, random);

        var humanWarrior = new Unit
        {
            GameId = gameId,
            ParticipantId = humanParticipantId,
            TypeId = warriorDef.Id,
            TileId = humanUnitTile?.Id ?? humanStartTile.Id, // Fallback to city tile if no adjacent found
            Hp = warriorDef.Health,
            HasActed = false
        };

        var aiWarrior = new Unit
        {
            GameId = gameId,
            ParticipantId = aiParticipantId,
            TypeId = warriorDef.Id,
            TileId = aiUnitTile?.Id ?? aiStartTile.Id, // Fallback to city tile if no adjacent found
            Hp = warriorDef.Health,
            HasActed = false
        };

        _context.Units.AddRange(humanWarrior, aiWarrior);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created starting units: human warrior={HumanUnitId}, ai warrior={AiUnitId}",
            humanWarrior.Id,
            aiWarrior.Id);

        _logger.LogInformation("Successfully seeded all entities for game {GameId}", gameId);
    }

    /// <summary>
    /// Determines starting positions for participants (maximally separated on the map).
    /// </summary>
    private static List<MapTile> DetermineStartingPositions(List<MapTile> tiles, int width, int height, Random random)
    {
        // Filter out water tiles (not suitable for city placement)
        var landTiles = tiles.Where(t => !TerrainTypes.IsWater(t.Terrain)).ToList();
        var validTiles = landTiles.Any() ? landTiles : tiles;

        if (validTiles.Count < 2)
        {
            throw new InvalidOperationException(
                $"Not enough valid tiles for starting positions. Found {validTiles.Count} valid tiles, need at least 2.");
        }

        // Get top 10 tiles from each corner and randomize selection
        var topLeftCandidates = validTiles
            .OrderBy(t => t.Row + t.Col)
            .Take(10)
            .ToList();

        var bottomRightCandidates = validTiles
            .OrderByDescending(t => t.Row + t.Col)
            .Take(10)
            .ToList();

        // Randomly select from candidates
        var topLeft = topLeftCandidates[random.Next(topLeftCandidates.Count)];
        var bottomRight = bottomRightCandidates[random.Next(bottomRightCandidates.Count)];

        // Ensure they're different tiles
        if (topLeft.Id == bottomRight.Id)
        {
            // If same tile selected, try to get a different one
            var alternativeCandidates = bottomRightCandidates.Where(t => t.Id != topLeft.Id).ToList();
            if (alternativeCandidates.Any())
            {
                bottomRight = alternativeCandidates[random.Next(alternativeCandidates.Count)];
            }
            else
            {
                // Fallback: shuffle all valid tiles and take first two
                var shuffled = validTiles.OrderBy(_ => random.Next()).ToList();
                return shuffled.Take(2).ToList();
            }
        }

        return new List<MapTile> { topLeft, bottomRight };
    }
}
