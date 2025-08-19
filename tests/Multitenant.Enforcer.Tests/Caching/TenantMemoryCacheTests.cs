using Microsoft.Extensions.Caching.Memory;
using Multitenant.Enforcer.Cache;

namespace MultiTenant.Enforcer.Tests.Caching;

public class TenantMemoryCacheTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly InMemoryTenantCache _cache;

    public TenantMemoryCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cache = new InMemoryTenantCache(_memoryCache);
    }

    [Fact]
    public async Task GetAsync_WithExistingKey_ReturnsValue()
    {
        // Arrange
        var key = "test_key";
        var value = "test_value";
        await _cache.SetAsync(key, value, TimeSpan.FromMinutes(5));

        // Act
        var result = await _cache.GetAsync<string>(key);

        // Assert
        result.ShouldBe(value);
    }

    [Fact]
    public async Task GetAsync_WithNonExistentKey_ReturnsDefault()
    {
        // Act
        var result = await _cache.GetAsync<string>("non_existent_key");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_WithWrongType_ReturnsDefault()
    {
        // Arrange
        var key = "test_key";
        await _cache.SetAsync(key, "string_value", TimeSpan.FromMinutes(5));

        // Act
        var result = await _cache.GetAsync<int>(key);

        // Assert
        result.ShouldBe(default(int));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_WithInvalidKey_ThrowsArgumentException(string invalidKey)
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => _cache.GetAsync<string>(invalidKey));
    }

    [Fact]
    public async Task GetAsync_WithCancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _cache.GetAsync<string>("key", cancellationToken));
    }

    [Fact]
    public async Task SetAsync_WithTimeSpan_StoresValue()
    {
        // Arrange
        var key = "test_key";
        var value = "test_value";

        // Act
        await _cache.SetAsync(key, value, TimeSpan.FromMinutes(5));

        // Assert
        var result = await _cache.GetAsync<string>(key);
        result.ShouldBe(value);
    }

    [Fact]
    public async Task SetAsync_WithNullExpiry_StoresValue()
    {
        // Arrange
        var key = "test_key";
        var value = "test_value";

        // Act
        await _cache.SetAsync(key, value, (TimeSpan?)null);

        // Assert
        var result = await _cache.GetAsync<string>(key);
        result.ShouldBe(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAsync_WithTimeSpan_WithInvalidKey_ThrowsArgumentException(string invalidKey)
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => _cache.SetAsync(invalidKey, "value", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task SetAsync_WithTimeSpan_WithCancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _cache.SetAsync("key", "value", TimeSpan.FromMinutes(5), cancellationToken));
    }

    [Fact]
    public async Task SetAsync_WithOptions_StoresValue()
    {
        // Arrange
        var key = "test_key";
        var value = "test_value";
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            Priority = CacheItemPriority.High
        };

        // Act
        await _cache.SetAsync(key, value, options);

        // Assert
        var result = await _cache.GetAsync<string>(key);
        result.ShouldBe(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAsync_WithOptions_WithInvalidKey_ThrowsArgumentException(string invalidKey)
    {
        // Arrange
        var options = new MemoryCacheEntryOptions();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => _cache.SetAsync(invalidKey, "value", options));
    }

    [Fact]
    public async Task SetAsync_WithOptions_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => _cache.SetAsync("key", "value", (MemoryCacheEntryOptions)null!));
    }

    [Fact]
    public async Task SetAsync_WithOptions_WithCancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true);
        var options = new MemoryCacheEntryOptions();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _cache.SetAsync("key", "value", options, cancellationToken));
    }

    [Fact]
    public async Task RemoveAsync_WithExistingKey_RemovesValue()
    {
        // Arrange
        var key = "test_key";
        await _cache.SetAsync(key, "test_value", TimeSpan.FromMinutes(5));

        // Act
        await _cache.RemoveAsync(key);

        // Assert
        var result = await _cache.GetAsync<string>(key);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAsync_WithNonExistentKey_DoesNotThrow()
    {
        // Act & Assert
        await Should.NotThrowAsync(() => _cache.RemoveAsync("non_existent_key"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveAsync_WithInvalidKey_ThrowsArgumentException(string invalidKey)
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => _cache.RemoveAsync(invalidKey));
    }

    [Fact]
    public async Task RemoveAsync_WithCancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _cache.RemoveAsync("key", cancellationToken));
    }

    [Fact]
    public async Task SetAndGet_WithComplexObject_WorksCorrectly()
    {
        // Arrange
        var key = "complex_key";
        var value = new { Id = 123, Name = "Test", Items = new[] { "A", "B", "C" } };

        // Act
        await _cache.SetAsync(key, value, TimeSpan.FromMinutes(5));
        var result = await _cache.GetAsync<object>(key);

        // Assert
        result.ShouldBe(value);
    }

    [Fact]
    public async Task SetAsync_OverwriteExistingKey_UpdatesValue()
    {
        // Arrange
        var key = "test_key";
        var originalValue = "original";
        var newValue = "updated";

        // Act
        await _cache.SetAsync(key, originalValue, TimeSpan.FromMinutes(5));
        await _cache.SetAsync(key, newValue, TimeSpan.FromMinutes(5));

        // Assert
        var result = await _cache.GetAsync<string>(key);
        result.ShouldBe(newValue);
    }

    [Fact]
    public async Task GetAsync_WithGenericType_WorksWithValueTypes()
    {
        // Arrange
        var key = "int_key";
        var value = 42;

        // Act
        await _cache.SetAsync(key, value, TimeSpan.FromMinutes(5));
        var result = await _cache.GetAsync<int>(key);

        // Assert
        result.ShouldBe(value);
    }

    [Fact]
    public async Task GetAsync_WithGenericType_WorksWithNullableTypes()
    {
        // Arrange
        var key = "nullable_key";
        int? value = 42;

        // Act
        await _cache.SetAsync(key, value, TimeSpan.FromMinutes(5));
        var result = await _cache.GetAsync<int?>(key);

        // Assert
        result.ShouldBe(value);
    }
}