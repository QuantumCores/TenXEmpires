using TenXEmpires.Server.Domain.Utilities;
using Xunit;

namespace TenXEmpires.Server.Tests.Utilities;

public class CubeCoordTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        // Act
        var coord = new CubeCoord(1, 2, -3);

        // Assert
        Assert.Equal(1, coord.X);
        Assert.Equal(2, coord.Y);
        Assert.Equal(-3, coord.Z);
    }

    [Fact]
    public void Addition_ShouldAddCoordinates()
    {
        // Arrange
        var a = new CubeCoord(1, 2, -3);
        var b = new CubeCoord(2, -1, -1);

        // Act
        var result = a + b;

        // Assert
        Assert.Equal(3, result.X);
        Assert.Equal(1, result.Y);
        Assert.Equal(-4, result.Z);
    }

    [Fact]
    public void Subtraction_ShouldSubtractCoordinates()
    {
        // Arrange
        var a = new CubeCoord(5, 3, -8);
        var b = new CubeCoord(2, 1, -3);

        // Act
        var result = a - b;

        // Assert
        Assert.Equal(3, result.X);
        Assert.Equal(2, result.Y);
        Assert.Equal(-5, result.Z);
    }

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var a = new CubeCoord(1, 2, -3);
        var b = new CubeCoord(1, 2, -3);

        // Act & Assert
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equals_WithDifferentValues_ShouldReturnFalse()
    {
        // Arrange
        var a = new CubeCoord(1, 2, -3);
        var b = new CubeCoord(1, 2, -2);

        // Act & Assert
        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_ForEqualObjects_ShouldBeEqual()
    {
        // Arrange
        var a = new CubeCoord(1, 2, -3);
        var b = new CubeCoord(1, 2, -3);

        // Act & Assert
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var coord = new CubeCoord(1, 2, -3);

        // Act
        var result = coord.ToString();

        // Assert
        Assert.Equal("Cube(1, 2, -3)", result);
    }
}

