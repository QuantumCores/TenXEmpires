using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Domain.Utilities;

/// <summary>
/// Utility class for hexagonal grid operations using odd-r offset coordinates.
/// </summary>
public static class HexagonalGrid
{
    /// <summary>
    /// Six directions in cube coordinate system.
    /// </summary>
    public static readonly CubeCoord[] CubeDirections = new CubeCoord[]
    {
        new CubeCoord(+1, 0, -1), new CubeCoord(+1, -1, 0), new CubeCoord(0, -1, +1),
        new CubeCoord(-1, 0, +1), new CubeCoord(-1, +1, 0), new CubeCoord(0, +1, -1)
    };

    /// <summary>
    /// Converts cube coordinates to odd-r offset coordinates.
    /// </summary>
    public static SquareCoord ConvertCubeToOddr(CubeCoord cube)
    {
        var col = cube.X + (cube.Z - (cube.Z & 1)) / 2;
        var row = cube.Z;
        return new SquareCoord(col, row);
    }

    /// <summary>
    /// Converts odd-r offset coordinates to cube coordinates.
    /// </summary>
    public static CubeCoord ConvertOddrToCube(int x, int y)
    {
        var cubeX = x - (y - (y & 1)) / 2;
        var cubeZ = y;
        var cubeY = -cubeX - cubeZ;
        return new CubeCoord(cubeX, cubeY, cubeZ);
    }

    /// <summary>
    /// Gets all neighbor coordinates in cube coordinate system.
    /// </summary>
    public static IEnumerable<CubeCoord> GetHexNeighbours(CubeCoord cube)
    {
        foreach (var dir in CubeDirections)
        {
            yield return cube + dir;
        }
    }

    /// <summary>
    /// Calculates the distance between two cube coordinates on a hexagonal grid.
    /// </summary>
    /// <param name="a">First cube coordinate.</param>
    /// <param name="b">Second cube coordinate.</param>
    /// <returns>The hexagonal distance between the two coordinates.</returns>
    public static int GetCubeDistance(CubeCoord a, CubeCoord b)
    {
        return (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z)) / 2;
    }

    /// <summary>
    /// Finds an adjacent tile to the given tile that's not in the exclusion set.
    /// </summary>
    /// <param name="centerTile">The center tile to find neighbors for.</param>
    /// <param name="allTiles">All available tiles on the map.</param>
    /// <param name="mapWidth">Width of the map.</param>
    /// <param name="mapHeight">Height of the map.</param>
    /// <param name="random">Random number generator for selection.</param>
    /// <param name="excludeTileIds">Optional set of tile IDs to exclude from selection.</param>
    /// <returns>A random adjacent tile, or null if none available.</returns>
    public static MapTile? FindAdjacentTile(
        MapTile centerTile,
        List<MapTile> allTiles,
        int mapWidth,
        int mapHeight,
        Random random,
        long[]? excludeTileIds = null)
    {
        var excludeSet = excludeTileIds != null 
            ? new HashSet<long>(excludeTileIds) 
            : new HashSet<long>();
        excludeSet.Add(centerTile.Id);

        // Convert center tile to cube coordinates
        var centerCube = ConvertOddrToCube(centerTile.Col, centerTile.Row);

        // Get all neighbor cube coordinates
        var neighborCubes = GetHexNeighbours(centerCube);

        var adjacentTiles = new List<MapTile>();

        foreach (var neighborCube in neighborCubes)
        {
            // Convert back to offset coordinates
            var neighborSquare = ConvertCubeToOddr(neighborCube);

            // Check bounds
            if (neighborSquare.Y < 0 || neighborSquare.Y >= mapHeight || 
                neighborSquare.X < 0 || neighborSquare.X >= mapWidth)
            {
                continue;
            }

            // Find the tile at this position
            var tile = allTiles.FirstOrDefault(t => 
                t.Row == neighborSquare.Y && t.Col == neighborSquare.X);

            if (tile != null && !excludeSet.Contains(tile.Id))
            {
                adjacentTiles.Add(tile);
            }
        }

        // Return random adjacent tile
        return adjacentTiles.Count > 0 
            ? adjacentTiles[random.Next(adjacentTiles.Count)] 
            : null;
    }
}

