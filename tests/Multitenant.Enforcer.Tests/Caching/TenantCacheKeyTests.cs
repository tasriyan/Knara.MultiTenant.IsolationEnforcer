using Multitenant.Enforcer.Cache;

namespace MultiTenant.Enforcer.Tests.Caching;

public class TenantCacheKeyTests
{
    [Fact]
    public void TenantDomainCacheKey_ImplicitOperator_ReturnsCorrectFormat()
    {
        // Arrange
        var domain = "example.com";
        var key = new TenantDomainCacheKey(domain);

        // Act
        string result = key;

        // Assert
        result.ShouldBe("tenant_domain_example.com");
    }

    [Fact]
    public void TenantDomainCacheKey_ImplicitOperator_ConvertsToLowerCase()
    {
        // Arrange
        var domain = "EXAMPLE.COM";
        var key = new TenantDomainCacheKey(domain);

        // Act
        string result = key;

        // Assert
        result.ShouldBe("tenant_domain_example.com");
    }

    [Theory]
    [InlineData("tenant1.com", "tenant_domain_tenant1.com")]
    [InlineData("api.tenant2.com", "tenant_domain_api.tenant2.com")]
    [InlineData("SUB.DOMAIN.COM", "tenant_domain_sub.domain.com")]
    [InlineData("localhost", "tenant_domain_localhost")]
    public void TenantDomainCacheKey_ImplicitOperator_HandlesVariousDomains(string domain, string expected)
    {
        // Arrange
        var key = new TenantDomainCacheKey(domain);

        // Act
        string result = key;

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void TenantInfoCacheKey_ImplicitOperator_ReturnsCorrectFormat()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var key = new TenantInfoCacheKey(tenantId);

        // Act
        string result = key;

        // Assert
        result.ShouldBe($"tenant_info_{tenantId}");
    }

    [Fact]
    public void TenantInfoCacheKey_ImplicitOperator_WithDifferentGuids_ProducesDifferentKeys()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var key1 = new TenantInfoCacheKey(tenantId1);
        var key2 = new TenantInfoCacheKey(tenantId2);

        // Act
        string result1 = key1;
        string result2 = key2;

        // Assert
        result1.ShouldNotBe(result2);
        result1.ShouldBe($"tenant_info_{tenantId1}");
        result2.ShouldBe($"tenant_info_{tenantId2}");
    }

    [Fact]
    public void TenantInfoCacheKey_ImplicitOperator_WithSameGuid_ProducesSameKey()
    {
        // Arrange
        var tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var key1 = new TenantInfoCacheKey(tenantId);
        var key2 = new TenantInfoCacheKey(tenantId);

        // Act
        string result1 = key1;
        string result2 = key2;

        // Assert
        result1.ShouldBe(result2);
        result1.ShouldBe("tenant_info_12345678-1234-1234-1234-123456789012");
    }
}