using Microsoft.Extensions.Caching.Memory;
using TenXEmpires.Server.Infrastructure.Services;
using Xunit;

namespace TenXEmpires.Server.Tests.Services;

public class MemoryIdempotencyStoreTests
{
    [Fact]
    public async Task TryStoreAsync_FirstTime_ShouldReturnTrue()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryIdempotencyStore(cache);
        var key = "test-key";
        var value = "test-value";

        // Act
        var result = await store.TryStoreAsync(key, value, TimeSpan.FromMinutes(1));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TryStoreAsync_SecondTime_ShouldReturnFalse()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryIdempotencyStore(cache);
        var key = "test-key";
        var value1 = "value1";
        var value2 = "value2";

        // Act
        var result1 = await store.TryStoreAsync(key, value1, TimeSpan.FromMinutes(1));
        var result2 = await store.TryStoreAsync(key, value2, TimeSpan.FromMinutes(1));

        // Assert
        Assert.True(result1);
        Assert.False(result2);
    }

    [Fact]
    public async Task TryGetAsync_ExistingKey_ShouldReturnValue()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryIdempotencyStore(cache);
        var key = "test-key";
        var value = "test-value";
        await store.TryStoreAsync(key, value, TimeSpan.FromMinutes(1));

        // Act
        var result = await store.TryGetAsync<string>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task TryGetAsync_NonExistingKey_ShouldReturnNull()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryIdempotencyStore(cache);
        var key = "non-existing-key";

        // Act
        var result = await store.TryGetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TryStoreAsync_WithExpiration_ShouldExpire()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryIdempotencyStore(cache);
        var key = "expiring-key";
        var value = "expiring-value";

        // Act
        await store.TryStoreAsync(key, value, TimeSpan.FromMilliseconds(100));
        await Task.Delay(200); // Wait for expiration
        var result = await store.TryGetAsync<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TryStoreAsync_WithComplexType_ShouldStoreAndRetrieve()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryIdempotencyStore(cache);
        var key = "complex-key";
        var value = new TestData { Id = 42, Name = "Test" };

        // Act
        await store.TryStoreAsync(key, value, TimeSpan.FromMinutes(1));
        var result = await store.TryGetAsync<TestData>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(42, result.Id);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public async Task TryStoreAsync_DifferentKeys_ShouldStoreIndependently()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryIdempotencyStore(cache);

        // Act
        var result1 = await store.TryStoreAsync("key1", "value1", TimeSpan.FromMinutes(1));
        var result2 = await store.TryStoreAsync("key2", "value2", TimeSpan.FromMinutes(1));
        var retrieved1 = await store.TryGetAsync<string>("key1");
        var retrieved2 = await store.TryGetAsync<string>("key2");

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.Equal("value1", retrieved1);
        Assert.Equal("value2", retrieved2);
    }

    private class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

