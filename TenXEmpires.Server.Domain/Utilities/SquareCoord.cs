namespace TenXEmpires.Server.Domain.Utilities;

/// <summary>
/// Represents a coordinate in square/offset coordinate system (row, col).
/// </summary>
public readonly struct SquareCoord : IEquatable<SquareCoord>
{
    public int X { get; }
    public int Y { get; }

    public SquareCoord(int x, int y)
    {
        X = x;
        Y = y;
    }

    public bool Equals(SquareCoord other) =>
        X == other.X && Y == other.Y;

    public override bool Equals(object? obj) =>
        obj is SquareCoord other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(X, Y);

    public static bool operator ==(SquareCoord left, SquareCoord right) =>
        left.Equals(right);

    public static bool operator !=(SquareCoord left, SquareCoord right) =>
        !left.Equals(right);

    public override string ToString() => $"Square({X}, {Y})";
}

