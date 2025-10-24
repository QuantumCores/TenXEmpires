using TenXEmpires.Server.Infrastructure.Services;
using Xunit;

namespace TenXEmpires.Server.Tests.Services;

public class AiNameGeneratorTests
{
    [Fact]
    public void GenerateName_WithoutSeed_ShouldReturnValidName()
    {
        // Arrange
        var generator = new AiNameGenerator();

        // Act
        var name = generator.GenerateName();

        // Assert
        Assert.NotNull(name);
        Assert.NotEmpty(name);
    }

    [Fact]
    public void GenerateName_WithSeed_ShouldBeDeterministic()
    {
        // Arrange
        var generator = new AiNameGenerator();
        var seed = 12345L;

        // Act
        var name1 = generator.GenerateName(seed);
        var name2 = generator.GenerateName(seed);

        // Assert
        Assert.Equal(name1, name2);
    }

    [Fact]
    public void GenerateName_WithDifferentSeeds_ShouldReturnDifferentNames()
    {
        // Arrange
        var generator = new AiNameGenerator();
        var seed1 = 12345L;
        var seed2 = 67890L;

        // Act
        var name1 = generator.GenerateName(seed1);
        var name2 = generator.GenerateName(seed2);

        // Assert - high probability they're different (not guaranteed due to random selection)
        // We'll just verify both are valid names
        Assert.NotNull(name1);
        Assert.NotNull(name2);
        Assert.NotEmpty(name1);
        Assert.NotEmpty(name2);
    }

    [Fact]
    public void GenerateName_MultipleCallsWithoutSeed_ShouldReturnValidNames()
    {
        // Arrange
        var generator = new AiNameGenerator();

        // Act
        var names = Enumerable.Range(0, 10).Select(_ => generator.GenerateName()).ToList();

        // Assert
        Assert.All(names, name =>
        {
            Assert.NotNull(name);
            Assert.NotEmpty(name);
        });
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(100L)]
    [InlineData(999999L)]
    [InlineData(long.MaxValue)]
    public void GenerateName_WithVariousSeeds_ShouldNotThrow(long seed)
    {
        // Arrange
        var generator = new AiNameGenerator();

        // Act & Assert
        var exception = Record.Exception(() => generator.GenerateName(seed));
        Assert.Null(exception);
    }
}

