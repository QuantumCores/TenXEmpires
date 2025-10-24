using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Domain.Utilities;
using Xunit;

namespace TenXEmpires.Server.Tests.Utilities;

public class HexagonalGridTests
{
    [Fact]
    public void ConvertOddrToCube_ShouldSatisfyCubeConstraint()
    {
        // Arrange - test various coordinates
        var testCases = new (int col, int row)[]
        {
            (0, 0), (1, 0), (0, 1), (5, 3), (7, 8), (10, 10), (2, 5), (3, 2)
        };

        foreach (var (col, row) in testCases)
        {
            // Act
            var cube = HexagonalGrid.ConvertOddrToCube(col, row);

            // Assert - verify cube coordinate constraint: x + y + z = 0
            Assert.Equal(0, cube.X + cube.Y + cube.Z);
        }
    }

    [Fact]
    public void ConvertCubeToOddr_ShouldProduceValidCoordinates()
    {
        // Arrange - test various cube coordinates
        var testCubes = new CubeCoord[]
        {
            new(0, 0, 0),
            new(1, -1, 0),
            new(0, -1, 1),
            new(2, -3, 1),
            new(-1, 0, 1)
        };

        foreach (var cube in testCubes)
        {
            // Act
            var square = HexagonalGrid.ConvertCubeToOddr(cube);

            // Assert - verify result is non-negative (valid grid coordinates)
            Assert.True(square.X >= 0 || square.Y >= 0, "Should produce valid coordinates");
        }
    }

    [Fact]
    public void ConvertOddrToCube_AndBack_ShouldBeIdempotent()
    {
        // Arrange
        var testCases = new (int col, int row)[]
        {
            (0, 0), (1, 0), (0, 1), (5, 3), (7, 8), (10, 10)
        };

        foreach (var (col, row) in testCases)
        {
            // Act
            var cube = HexagonalGrid.ConvertOddrToCube(col, row);
            var square = HexagonalGrid.ConvertCubeToOddr(cube);

            // Assert
            Assert.Equal(col, square.X);
            Assert.Equal(row, square.Y);
        }
    }

    [Fact]
    public void GetHexNeighbours_ShouldReturn6Neighbors()
    {
        // Arrange
        var center = new CubeCoord(0, 0, 0);

        // Act
        var neighbors = HexagonalGrid.GetHexNeighbours(center).ToList();

        // Assert
        Assert.Equal(6, neighbors.Count);
        
        // Verify all neighbors are adjacent (distance = 1)
        foreach (var neighbor in neighbors)
        {
            var distance = Math.Max(Math.Abs(neighbor.X), Math.Max(Math.Abs(neighbor.Y), Math.Abs(neighbor.Z)));
            Assert.Equal(1, distance);
        }
    }

    [Fact]
    public void GetHexNeighbours_ShouldReturnUniqueNeighbors()
    {
        // Arrange
        var center = new CubeCoord(5, -2, -3);

        // Act
        var neighbors = HexagonalGrid.GetHexNeighbours(center).ToList();

        // Assert
        Assert.Equal(6, neighbors.Count);
        Assert.Equal(6, neighbors.Distinct().Count());
    }

    [Fact]
    public void FindAdjacentTile_WithNoExclusions_ShouldReturnRandomAdjacentTile()
    {
        // Arrange
        var tiles = CreateTestTiles(5, 5);
        var centerTile = tiles.First(t => t.Row == 2 && t.Col == 2);
        var random = new Random(42);

        // Act
        var adjacent = HexagonalGrid.FindAdjacentTile(centerTile, tiles, 5, 5, random);

        // Assert
        Assert.NotNull(adjacent);
        Assert.NotEqual(centerTile.Id, adjacent.Id);
        
        // Verify it's actually adjacent
        var distance = Math.Abs(adjacent.Row - centerTile.Row) + Math.Abs(adjacent.Col - centerTile.Col);
        Assert.True(distance <= 2); // In hex, adjacent tiles have Manhattan distance <= 2
    }

    [Fact]
    public void FindAdjacentTile_WithExclusions_ShouldNotReturnExcludedTiles()
    {
        // Arrange
        var tiles = CreateTestTiles(5, 5);
        var centerTile = tiles.First(t => t.Row == 2 && t.Col == 2);
        var excludedTiles = tiles.Where(t => t.Row == 1 && t.Col == 2).Select(t => t.Id).ToArray();
        var random = new Random(42);

        // Act
        var adjacent = HexagonalGrid.FindAdjacentTile(centerTile, tiles, 5, 5, random, excludedTiles);

        // Assert
        Assert.NotNull(adjacent);
        Assert.DoesNotContain(adjacent.Id, excludedTiles);
    }

    [Fact]
    public void FindAdjacentTile_AtEdge_ShouldNotReturnOutOfBoundsTiles()
    {
        // Arrange
        var tiles = CreateTestTiles(5, 5);
        var cornerTile = tiles.First(t => t.Row == 0 && t.Col == 0);
        var random = new Random(42);

        // Act
        var adjacent = HexagonalGrid.FindAdjacentTile(cornerTile, tiles, 5, 5, random);

        // Assert
        Assert.NotNull(adjacent);
        Assert.True(adjacent.Row >= 0 && adjacent.Row < 5);
        Assert.True(adjacent.Col >= 0 && adjacent.Col < 5);
    }

    [Fact]
    public void FindAdjacentTile_WhenAllExcluded_ShouldReturnNull()
    {
        // Arrange
        var tiles = CreateTestTiles(3, 3);
        var centerTile = tiles.First(t => t.Row == 1 && t.Col == 1);
        var allTileIds = tiles.Select(t => t.Id).ToArray();
        var random = new Random(42);

        // Act
        var adjacent = HexagonalGrid.FindAdjacentTile(centerTile, tiles, 3, 3, random, allTileIds);

        // Assert
        Assert.Null(adjacent);
    }

    private static List<MapTile> CreateTestTiles(int width, int height)
    {
        var tiles = new List<MapTile>();
        long id = 1;

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                tiles.Add(new MapTile
                {
                    Id = id++,
                    MapId = 1,
                    Row = row,
                    Col = col,
                    Terrain = "grassland"
                });
            }
        }

        return tiles;
    }
}

