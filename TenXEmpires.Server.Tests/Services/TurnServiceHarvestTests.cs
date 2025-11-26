using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using TenXEmpires.Server.Domain.Constants;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Infrastructure.Data;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Services;

/// <summary>
/// Unit tests for TurnService harvest functionality with storage cap enforcement.
/// </summary>
public class TurnServiceHarvestTests
{
    private static async Task<TenXDbContext> CreateTestContextAsync()
    {
        var options = new DbContextOptionsBuilder<TenXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new TenXDbContext(options);
        await Task.CompletedTask;
        return context;
    }

    private static TurnService CreateTurnService(TenXDbContext context)
    {
        return new TurnService(context, Mock.Of<ILogger<TurnService>>());
    }

    private static Dictionary<string, int> CreateEmptyTotals()
    {
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { ResourceTypes.Wood, 0 },
            { ResourceTypes.Stone, 0 },
            { ResourceTypes.Wheat, 0 },
            { ResourceTypes.Iron, 0 }
        };
    }

    private static async Task<(City city, MapTile resourceTile, GameTileState tileState, TenXDbContext context)> SetupSingleResourceTileAsync(
        string resourceType,
        int initialAmount,
        int tileResourceAmount = 50)
    {
        var context = await CreateTestContextAsync();

        var map = new Map { Id = 1, Code = "test", SchemaVersion = 1, Width = 3, Height = 3 };
        context.Maps.Add(map);

        var tiles = new List<MapTile>();
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                tiles.Add(new MapTile { Id = r * 3 + c + 1, MapId = 1, Row = r, Col = c, Terrain = "plains" });
            }
        }
        context.MapTiles.AddRange(tiles);

        var participant = new Participant { Id = 1, GameId = 1, Kind = ParticipantKind.Human, UserId = Guid.NewGuid(), DisplayName = "Player" };
        context.Participants.Add(participant);

        var game = new Game { Id = 1, UserId = participant.UserId!.Value, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active, ActiveParticipantId = participant.Id };
        context.Games.Add(game);

        var cityTile = tiles.First(t => t.Row == 1 && t.Col == 1);
        var resourceTile = tiles.First(t => t.Row == 0 && t.Col == 0);
        resourceTile.ResourceType = resourceType;
        resourceTile.ResourceAmount = tileResourceAmount;

        var city = new City { Id = 1, GameId = 1, ParticipantId = participant.Id, TileId = cityTile.Id, Hp = 100, MaxHp = 100 };
        context.Cities.Add(city);

        context.CityTiles.Add(new CityTile { GameId = 1, CityId = city.Id, TileId = resourceTile.Id });
        context.CityResources.Add(new CityResource { CityId = city.Id, ResourceType = resourceType, Amount = initialAmount });

        foreach (var tile in tiles)
        {
            context.GameTileStates.Add(new GameTileState
            {
                GameId = game.Id,
                TileId = tile.Id,
                ResourceAmount = tile.ResourceAmount
            });
        }

        await context.SaveChangesAsync();

        var tileState = await context.GameTileStates.SingleAsync(ts => ts.GameId == game.Id && ts.TileId == resourceTile.Id);

        return (city, resourceTile, tileState, context);
    }

    private static System.Reflection.MethodInfo GetHarvestMethod()
    {
        var method = typeof(TurnService).GetMethod(
            "HarvestCityResources",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return method!;
    }

    // === Happy Path Tests ===

    [Fact]
    public async Task HarvestCityResources_BelowCap_HarvestsNormally()
    {
        // Given: City has 50 wheat, tile has wheat resource
        var (city, resourceTile, tileState, context) = await SetupSingleResourceTileAsync(ResourceTypes.Wheat, 50);

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var allUnits = new List<Unit>();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        // When: End turn is processed
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 100 });

        // Then: City wheat increases to 51
        loadedCity.CityResources.First(r => r.ResourceType == ResourceTypes.Wheat).Amount.Should().Be(51);
        harvestedTotals[ResourceTypes.Wheat].Should().Be(1);
        overflowTotals[ResourceTypes.Wheat].Should().Be(0);
    }

    [Fact]
    public async Task HarvestCityResources_AtCap_DoesNotHarvest()
    {
        // Given: City has 100 wheat (at cap), tile has wheat resource
        var (city, resourceTile, tileState, context) = await SetupSingleResourceTileAsync(ResourceTypes.Wheat, 100);

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var allUnits = new List<Unit>();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        // When: End turn is processed
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 100 });

        // Then: City wheat remains 100, overflow is tracked
        loadedCity.CityResources.First(r => r.ResourceType == ResourceTypes.Wheat).Amount.Should().Be(100);
        harvestedTotals[ResourceTypes.Wheat].Should().Be(0);
        overflowTotals[ResourceTypes.Wheat].Should().Be(1);
    }

    [Fact]
    public async Task HarvestCityResources_NearCap_PartialHarvest()
    {
        // Given: City has 99 wheat, tile has wheat resource
        var (city, resourceTile, tileState, context) = await SetupSingleResourceTileAsync(ResourceTypes.Wheat, 99);

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var allUnits = new List<Unit>();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        // When: End turn is processed
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 100 });

        // Then: City wheat becomes 100, no overflow (since we harvest 1 and have space for 1)
        loadedCity.CityResources.First(r => r.ResourceType == ResourceTypes.Wheat).Amount.Should().Be(100);
        harvestedTotals[ResourceTypes.Wheat].Should().Be(1);
        overflowTotals[ResourceTypes.Wheat].Should().Be(0);
    }

    [Fact]
    public async Task HarvestCityResources_MultipleResources_IndependentCaps()
    {
        // Given: City has 100 wheat (at cap), 50 wood, tiles have both
        var context = await CreateTestContextAsync();

        var map = new Map { Id = 1, Code = "test", SchemaVersion = 1, Width = 3, Height = 3 };
        context.Maps.Add(map);

        var tiles = new List<MapTile>();
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                tiles.Add(new MapTile { Id = r * 3 + c + 1, MapId = 1, Row = r, Col = c, Terrain = "plains" });
            }
        }
        context.MapTiles.AddRange(tiles);

        var participant = new Participant { Id = 1, GameId = 1, Kind = ParticipantKind.Human, UserId = Guid.NewGuid(), DisplayName = "Player" };
        context.Participants.Add(participant);

        var game = new Game { Id = 1, UserId = participant.UserId!.Value, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active, ActiveParticipantId = participant.Id };
        context.Games.Add(game);

        var cityTile = tiles.First(t => t.Row == 1 && t.Col == 1);
        var wheatTile = tiles.First(t => t.Row == 0 && t.Col == 0);
        var woodTile = tiles.First(t => t.Row == 0 && t.Col == 1);
        wheatTile.ResourceType = ResourceTypes.Wheat;
        wheatTile.ResourceAmount = 50;
        woodTile.ResourceType = ResourceTypes.Wood;
        woodTile.ResourceAmount = 50;

        var city = new City { Id = 1, GameId = 1, ParticipantId = participant.Id, TileId = cityTile.Id, Hp = 100, MaxHp = 100 };
        context.Cities.Add(city);

        context.CityTiles.Add(new CityTile { GameId = 1, CityId = city.Id, TileId = wheatTile.Id });
        context.CityTiles.Add(new CityTile { GameId = 1, CityId = city.Id, TileId = woodTile.Id });
        context.CityResources.Add(new CityResource { CityId = city.Id, ResourceType = ResourceTypes.Wheat, Amount = 100 });
        context.CityResources.Add(new CityResource { CityId = city.Id, ResourceType = ResourceTypes.Wood, Amount = 50 });

        foreach (var tile in tiles)
        {
            context.GameTileStates.Add(new GameTileState
            {
                GameId = game.Id,
                TileId = tile.Id,
                ResourceAmount = tile.ResourceAmount
            });
        }

        await context.SaveChangesAsync();

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var allUnits = new List<Unit>();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        // When: End turn is processed
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 100 });

        // Then: Wheat unchanged (at cap), wood increases to 51
        loadedCity.CityResources.First(r => r.ResourceType == ResourceTypes.Wheat).Amount.Should().Be(100);
        loadedCity.CityResources.First(r => r.ResourceType == ResourceTypes.Wood).Amount.Should().Be(51);
        harvestedTotals[ResourceTypes.Wheat].Should().Be(0);
        harvestedTotals[ResourceTypes.Wood].Should().Be(1);
        overflowTotals[ResourceTypes.Wheat].Should().Be(1);
        overflowTotals[ResourceTypes.Wood].Should().Be(0);
    }

    // === Edge Cases ===

    [Fact]
    public async Task HarvestCityResources_MultipleTiles_CapsAtTotal()
    {
        // Given: City has 98 wheat, 3 tiles with wheat
        var context = await CreateTestContextAsync();

        var map = new Map { Id = 1, Code = "test", SchemaVersion = 1, Width = 4, Height = 4 };
        context.Maps.Add(map);

        var tiles = new List<MapTile>();
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                tiles.Add(new MapTile { Id = r * 4 + c + 1, MapId = 1, Row = r, Col = c, Terrain = "plains" });
            }
        }
        context.MapTiles.AddRange(tiles);

        var participant = new Participant { Id = 1, GameId = 1, Kind = ParticipantKind.Human, UserId = Guid.NewGuid(), DisplayName = "Player" };
        context.Participants.Add(participant);

        var game = new Game { Id = 1, UserId = participant.UserId!.Value, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active, ActiveParticipantId = participant.Id };
        context.Games.Add(game);

        var cityTile = tiles.First(t => t.Row == 1 && t.Col == 1);
        var wheatTile1 = tiles.First(t => t.Row == 0 && t.Col == 0);
        var wheatTile2 = tiles.First(t => t.Row == 0 && t.Col == 1);
        var wheatTile3 = tiles.First(t => t.Row == 0 && t.Col == 2);
        wheatTile1.ResourceType = ResourceTypes.Wheat;
        wheatTile1.ResourceAmount = 50;
        wheatTile2.ResourceType = ResourceTypes.Wheat;
        wheatTile2.ResourceAmount = 50;
        wheatTile3.ResourceType = ResourceTypes.Wheat;
        wheatTile3.ResourceAmount = 50;

        var city = new City { Id = 1, GameId = 1, ParticipantId = participant.Id, TileId = cityTile.Id, Hp = 100, MaxHp = 100 };
        context.Cities.Add(city);

        context.CityTiles.Add(new CityTile { GameId = 1, CityId = city.Id, TileId = wheatTile1.Id });
        context.CityTiles.Add(new CityTile { GameId = 1, CityId = city.Id, TileId = wheatTile2.Id });
        context.CityTiles.Add(new CityTile { GameId = 1, CityId = city.Id, TileId = wheatTile3.Id });
        context.CityResources.Add(new CityResource { CityId = city.Id, ResourceType = ResourceTypes.Wheat, Amount = 98 });

        foreach (var tile in tiles)
        {
            context.GameTileStates.Add(new GameTileState
            {
                GameId = game.Id,
                TileId = tile.Id,
                ResourceAmount = tile.ResourceAmount
            });
        }

        await context.SaveChangesAsync();

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var allUnits = new List<Unit>();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        // When: End turn is processed
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 100 });

        // Then: City wheat becomes 100, 1 overflow (harvested 2, overflow 1)
        loadedCity.CityResources.First(r => r.ResourceType == ResourceTypes.Wheat).Amount.Should().Be(100);
        harvestedTotals[ResourceTypes.Wheat].Should().Be(2);
        overflowTotals[ResourceTypes.Wheat].Should().Be(1);
    }

    [Fact]
    public async Task HarvestCityResources_EnemyOnTile_SkipsHarvest()
    {
        // Given: City has 50 wheat, enemy unit on wheat tile
        var (city, resourceTile, tileState, context) = await SetupSingleResourceTileAsync(ResourceTypes.Wheat, 50);

        var aiParticipant = new Participant { Id = 2, GameId = 1, Kind = ParticipantKind.Ai, DisplayName = "AI" };
        context.Participants.Add(aiParticipant);

        var warrior = new UnitDefinition { Id = 1, Code = UnitTypes.Warrior, Attack = 10, Defence = 10, Health = 100 };
        context.UnitDefinitions.Add(warrior);

        var enemyUnit = new Unit
        {
            Id = 1,
            GameId = 1,
            ParticipantId = aiParticipant.Id,
            TileId = resourceTile.Id,
            Tile = resourceTile,
            Hp = 100,
            TypeId = warrior.Id,
            Type = warrior
        };
        context.Units.Add(enemyUnit);
        await context.SaveChangesAsync();

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var allUnits = await context.Units.Include(u => u.Tile).ToListAsync();

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        // When: End turn is processed
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 100 });

        // Then: City wheat unchanged
        loadedCity.CityResources.First(r => r.ResourceType == ResourceTypes.Wheat).Amount.Should().Be(50);
        harvestedTotals[ResourceTypes.Wheat].Should().Be(0);
        overflowTotals[ResourceTypes.Wheat].Should().Be(0);
    }

    [Fact]
    public async Task HarvestCityResources_DepletedTile_SkipsHarvest()
    {
        // Given: Tile has ResourceAmount = 0
        var (city, resourceTile, tileState, context) = await SetupSingleResourceTileAsync(ResourceTypes.Wheat, 50, tileResourceAmount: 0);

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var allUnits = new List<Unit>();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        // Ensure tile state has 0 resource
        tileStateDict[resourceTile.Id].ResourceAmount = 0;

        // When: End turn is processed
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 100 });

        // Then: No harvest from that tile
        loadedCity.CityResources.First(r => r.ResourceType == ResourceTypes.Wheat).Amount.Should().Be(50);
        harvestedTotals[ResourceTypes.Wheat].Should().Be(0);
    }

    [Fact]
    public async Task HarvestCityResources_NoResourceTile_SkipsHarvest()
    {
        // Given: Tile has ResourceType = null
        var context = await CreateTestContextAsync();

        var map = new Map { Id = 1, Code = "test", SchemaVersion = 1, Width = 3, Height = 3 };
        context.Maps.Add(map);

        var tiles = new List<MapTile>();
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                tiles.Add(new MapTile { Id = r * 3 + c + 1, MapId = 1, Row = r, Col = c, Terrain = "plains" });
            }
        }
        context.MapTiles.AddRange(tiles);

        var participant = new Participant { Id = 1, GameId = 1, Kind = ParticipantKind.Human, UserId = Guid.NewGuid(), DisplayName = "Player" };
        context.Participants.Add(participant);

        var game = new Game { Id = 1, UserId = participant.UserId!.Value, MapId = 1, MapSchemaVersion = 1, TurnNo = 1, Status = GameStatus.Active, ActiveParticipantId = participant.Id };
        context.Games.Add(game);

        var cityTile = tiles.First(t => t.Row == 1 && t.Col == 1);
        var noResourceTile = tiles.First(t => t.Row == 0 && t.Col == 0);
        // ResourceType is null by default

        var city = new City { Id = 1, GameId = 1, ParticipantId = participant.Id, TileId = cityTile.Id, Hp = 100, MaxHp = 100 };
        context.Cities.Add(city);

        context.CityTiles.Add(new CityTile { GameId = 1, CityId = city.Id, TileId = noResourceTile.Id });

        foreach (var tile in tiles)
        {
            context.GameTileStates.Add(new GameTileState
            {
                GameId = game.Id,
                TileId = tile.Id,
                ResourceAmount = tile.ResourceAmount
            });
        }

        await context.SaveChangesAsync();

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var allUnits = new List<Unit>();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        // When: End turn is processed
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 100 });

        // Then: No harvest from that tile
        loadedCity.CityResources.Should().BeEmpty();
        harvestedTotals.Values.Sum().Should().Be(0);
    }

    // === Configuration Tests ===

    [Fact]
    public async Task HarvestCityResources_UsesConfiguredCap()
    {
        // Given: Custom storage cap of 50, city has 50 wheat
        var (city, resourceTile, tileState, context) = await SetupSingleResourceTileAsync(ResourceTypes.Wheat, 50);

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var allUnits = new List<Unit>();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        // When: Harvest runs with custom cap of 50
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 50 });

        // Then: Wheat does not increase, overflow tracked
        loadedCity.CityResources.First(r => r.ResourceType == ResourceTypes.Wheat).Amount.Should().Be(50);
        harvestedTotals[ResourceTypes.Wheat].Should().Be(0);
        overflowTotals[ResourceTypes.Wheat].Should().Be(1);
    }

    [Fact]
    public void DefaultStorageCap_IsOneHundred()
    {
        // Given/When: Checking the default storage cap constant
        // Then: It should be 100
        ResourceTypes.DefaultStorageCap.Should().Be(100);
    }

    [Fact]
    public async Task HarvestCityResources_TileResourceConsumption_WhenBelowCap()
    {
        // Given: City has 50 wheat, tile has 10 wheat resource
        var (city, resourceTile, tileState, context) = await SetupSingleResourceTileAsync(ResourceTypes.Wheat, 50, tileResourceAmount: 10);

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var allUnits = new List<Unit>();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        var initialTileResource = tileStateDict[resourceTile.Id].ResourceAmount;

        // When: End turn is processed
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 100 });

        // Then: Tile resource is consumed
        tileStateDict[resourceTile.Id].ResourceAmount.Should().Be(initialTileResource - 1);
    }

    [Fact]
    public async Task HarvestCityResources_TileResourceNotConsumed_WhenAtCap()
    {
        // Given: City has 100 wheat (at cap), tile has 10 wheat resource
        var (city, resourceTile, tileState, context) = await SetupSingleResourceTileAsync(ResourceTypes.Wheat, 100, tileResourceAmount: 10);

        var loadedCity = await context.Cities
            .Include(c => c.CityResources)
            .Include(c => c.CityTiles)
                .ThenInclude(ct => ct.Tile)
            .FirstAsync(c => c.Id == city.Id);

        var turnService = CreateTurnService(context);
        var method = GetHarvestMethod();
        var allUnits = new List<Unit>();
        var harvestedTotals = CreateEmptyTotals();
        var overflowTotals = CreateEmptyTotals();
        var tileStateDict = await context.GameTileStates.ToDictionaryAsync(ts => ts.TileId);

        var initialTileResource = tileStateDict[resourceTile.Id].ResourceAmount;

        // When: End turn is processed
        method.Invoke(turnService, new object[] { loadedCity, allUnits, harvestedTotals, overflowTotals, tileStateDict, 100 });

        // Then: Tile resource is NOT consumed when at cap
        tileStateDict[resourceTile.Id].ResourceAmount.Should().Be(initialTileResource);
    }
}

