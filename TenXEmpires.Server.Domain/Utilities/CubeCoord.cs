namespace TenXEmpires.Server.Domain.Utilities;

/// <summary>
/// Represents a hexagonal coordinate in cube coordinate system.
/// </summary>
public readonly struct CubeCoord : IEquatable<CubeCoord>
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public CubeCoord(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static CubeCoord operator +(CubeCoord a, CubeCoord b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static CubeCoord operator -(CubeCoord a, CubeCoord b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public bool Equals(CubeCoord other) =>
        X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) =>
        obj is CubeCoord other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(X, Y, Z);

    public static bool operator ==(CubeCoord left, CubeCoord right) =>
        left.Equals(right);

    public static bool operator !=(CubeCoord left, CubeCoord right) =>
        !left.Equals(right);

    public override string ToString() => $"Cube({X}, {Y}, {Z})";
}

