using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Domain.Utilities;

/// <summary>
/// Helper class for pathfinding operations using A* algorithm on hexagonal grids.
/// Uses cube coordinates internally for accurate hexagonal distance calculations.
/// </summary>
public static class PathfindingHelper
{
    /// <summary>
    /// Finds the shortest path from start to end position using A* with uniform cost on a hexagonal grid.
    /// </summary>
    /// <param name="start">Starting grid position (odd-r offset coordinates).</param>
    /// <param name="end">Target grid position (odd-r offset coordinates).</param>
    /// <param name="maxMovePoints">Maximum movement points available for the unit.</param>
    /// <param name="mapWidth">Width of the map (for boundary checking).</param>
    /// <param name="mapHeight">Height of the map (for boundary checking).</param>
    /// <param name="isBlocked">Function to check if a position is blocked (returns true if blocked).</param>
    /// <returns>The path as a list of grid positions, or null if no path exists within movement range.</returns>
    public static List<GridPosition>? FindPath(
        GridPosition start,
        GridPosition end,
        int maxMovePoints,
        int mapWidth,
        int mapHeight,
        Func<GridPosition, bool> isBlocked)
    {
        // Quick validation
        if (start.Row == end.Row && start.Col == end.Col)
        {
            return new List<GridPosition> { start };
        }

        if (!IsInBounds(end, mapWidth, mapHeight))
        {
            return null;
        }

        if (isBlocked(end))
        {
            return null;
        }

        // Convert start and end to cube coordinates for hexagonal calculations
        var startCube = HexagonalGrid.ConvertOddrToCube(start.Col, start.Row);
        var endCube = HexagonalGrid.ConvertOddrToCube(end.Col, end.Row);

        // Priority queue for A* (min-heap by f-score)
        var openSet = new PriorityQueue<Node, int>();
        var openSetLookup = new HashSet<CubeCoord>();
        var closedSet = new HashSet<CubeCoord>();
        var cameFrom = new Dictionary<CubeCoord, CubeCoord>();
        var gScore = new Dictionary<CubeCoord, int>();

        gScore[startCube] = 0;
        var startNode = new Node(startCube, 0, HexagonalGrid.GetCubeDistance(startCube, endCube));
        openSet.Enqueue(startNode, startNode.FScore);
        openSetLookup.Add(startCube);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            var currentCube = current.Position;
            openSetLookup.Remove(currentCube);

            // Check if we've reached the destination
            if (currentCube == endCube)
            {
                return ReconstructPath(cameFrom, currentCube, startCube);
            }

            closedSet.Add(currentCube);

            // Explore hexagonal neighbors
            var neighbors = HexagonalGrid.GetHexNeighbours(currentCube);

            foreach (var neighborCube in neighbors)
            {
                if (closedSet.Contains(neighborCube))
                {
                    continue;
                }

                // Convert neighbor to offset coordinates for bounds and blocking checks
                var neighborSquare = HexagonalGrid.ConvertCubeToOddr(neighborCube);
                
                // Check bounds
                if (!IsInBounds(neighborSquare, mapWidth, mapHeight))
                {
                    continue;
                }

                // Convert to GridPosition for blocking check
                var neighborGridPos = new GridPosition(neighborSquare.Y, neighborSquare.X);

                // Check if tile is blocked (but allow destination even if occupied)
                if (isBlocked(neighborGridPos) && neighborCube != endCube)
                {
                    continue;
                }

                // Calculate tentative g-score (uniform cost of 1 per tile)
                var tentativeGScore = gScore[currentCube] + 1;

                // Check if this path exceeds movement range
                if (tentativeGScore > maxMovePoints)
                {
                    continue;
                }

                // Check if this is a better path
                if (!gScore.ContainsKey(neighborCube) || tentativeGScore < gScore[neighborCube])
                {
                    cameFrom[neighborCube] = currentCube;
                    gScore[neighborCube] = tentativeGScore;

                    var hScore = HexagonalGrid.GetCubeDistance(neighborCube, endCube);
                    var fScore = tentativeGScore + hScore;

                    if (!openSetLookup.Contains(neighborCube))
                    {
                        var neighborNode = new Node(neighborCube, tentativeGScore, hScore);
                        openSet.Enqueue(neighborNode, fScore);
                        openSetLookup.Add(neighborCube);
                    }
                }
            }
        }

        // No path found within movement range
        return null;
    }

    /// <summary>
    /// Checks if a position is within map bounds.
    /// </summary>
    private static bool IsInBounds(GridPosition position, int mapWidth, int mapHeight)
    {
        return position.Row >= 0 && position.Row < mapHeight
            && position.Col >= 0 && position.Col < mapWidth;
    }

    /// <summary>
    /// Checks if a square coordinate position is within map bounds.
    /// </summary>
    private static bool IsInBounds(SquareCoord position, int mapWidth, int mapHeight)
    {
        return position.Y >= 0 && position.Y < mapHeight
            && position.X >= 0 && position.X < mapWidth;
    }

    /// <summary>
    /// Reconstructs the path from start to end using the cameFrom dictionary.
    /// Converts cube coordinates back to GridPosition for the API layer.
    /// </summary>
    private static List<GridPosition> ReconstructPath(
        Dictionary<CubeCoord, CubeCoord> cameFrom,
        CubeCoord current,
        CubeCoord start)
    {
        var path = new List<GridPosition>();
        var currentCube = current;

        // Build path from end to start
        var currentSquare = HexagonalGrid.ConvertCubeToOddr(currentCube);
        path.Add(new GridPosition(currentSquare.Y, currentSquare.X));

        while (cameFrom.ContainsKey(currentCube))
        {
            currentCube = cameFrom[currentCube];
            currentSquare = HexagonalGrid.ConvertCubeToOddr(currentCube);
            path.Add(new GridPosition(currentSquare.Y, currentSquare.X));
        }

        // Reverse to get path from start to end
        path.Reverse();

        return path;
    }

    /// <summary>
    /// Internal node representation for A* algorithm using cube coordinates.
    /// </summary>
    private record Node(CubeCoord Position, int GScore, int HScore)
    {
        public int FScore => GScore + HScore;
    }
}

