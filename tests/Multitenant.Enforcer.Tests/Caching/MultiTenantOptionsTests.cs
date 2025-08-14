using Multitenant.Enforcer;
using Multitenant.Enforcer.Caching;
using Multitenant.Enforcer.Resolvers;

namespace MultiTenant.Enforcer.Tests.Caching;

public class MultiTenantOptionsTests
{
    [Fact]
    public void UseJwtTenantResolver_WithoutConfiguration_SetsDefaultResolverAndReturnsOptions()
    {
        // Arrange
        var options = new MultiTenantOptions();

        // Act
        var result = options.UseJwtTenantResolver();

        // Assert
        result.ShouldBe(options);
        options.DefaultTenantResolver.ShouldBe(typeof(JwtTenantResolver));
    }

    [Fact]
    public void UseJwtTenantResolver_WithConfiguration_SetsDefaultResolverAndCallsConfiguration()
    {
        // Arrange
        var options = new MultiTenantOptions();
        var configurationCalled = false;

        // Act
        var result = options.UseJwtTenantResolver(jwtOptions =>
        {
            configurationCalled = true;
            jwtOptions.ShouldNotBeNull();
        });

        // Assert
        result.ShouldBe(options);
        options.DefaultTenantResolver.ShouldBe(typeof(JwtTenantResolver));
        configurationCalled.ShouldBeTrue();
    }

    [Fact]
    public void UseJwtTenantResolver_WithNullConfiguration_DoesNotThrow()
    {
        // Arrange
        var options = new MultiTenantOptions();

        // Act & Assert
        Should.NotThrow(() => options.UseJwtTenantResolver(null));
        options.DefaultTenantResolver.ShouldBe(typeof(JwtTenantResolver));
    }

    [Fact]
    public void UseSubdomainTenantResolver_WithoutConfiguration_SetsDefaultResolverAndReturnsOptions()
    {
        // Arrange
        var options = new MultiTenantOptions();

        // Act
        var result = options.UseSubdomainTenantResolver();

        // Assert
        result.ShouldBe(options);
        options.DefaultTenantResolver.ShouldBe(typeof(SubdomainTenantResolver));
    }

    [Fact]
    public void UseSubdomainTenantResolver_WithConfiguration_SetsDefaultResolverAndCallsConfiguration()
    {
        // Arrange
        var options = new MultiTenantOptions();
        var configurationCalled = false;

        // Act
        var result = options.UseSubdomainTenantResolver(subdomainOptions =>
        {
            configurationCalled = true;
            subdomainOptions.ShouldNotBeNull();
        });

        // Assert
        result.ShouldBe(options);
        options.DefaultTenantResolver.ShouldBe(typeof(SubdomainTenantResolver));
        configurationCalled.ShouldBeTrue();
    }

    [Fact]
    public void UseSubdomainTenantResolver_WithNullConfiguration_DoesNotThrow()
    {
        // Arrange
        var options = new MultiTenantOptions();

        // Act & Assert
        Should.NotThrow(() => options.UseSubdomainTenantResolver(null));
        options.DefaultTenantResolver.ShouldBe(typeof(SubdomainTenantResolver));
    }

    [Fact]
    public void UseCompositeResolver_WithSingleResolverType_SetsCustomResolvers()
    {
        // Arrange
        var options = new MultiTenantOptions();
        var resolverType = typeof(JwtTenantResolver);

        // Act
        var result = options.UseCompositeResolver(resolverType);

        // Assert
        result.ShouldBe(options);
        options.CustomTenantResolvers.ShouldNotBeNull();
        options.CustomTenantResolvers.Length.ShouldBe(1);
        options.CustomTenantResolvers[0].ShouldBe(resolverType);
    }

    [Fact]
    public void UseCompositeResolver_WithMultipleResolverTypes_SetsCustomResolvers()
    {
        // Arrange
        var options = new MultiTenantOptions();
        var resolverTypes = new[] { typeof(JwtTenantResolver), typeof(SubdomainTenantResolver) };

        // Act
        var result = options.UseCompositeResolver(resolverTypes);

        // Assert
        result.ShouldBe(options);
        options.CustomTenantResolvers.ShouldNotBeNull();
        options.CustomTenantResolvers.Length.ShouldBe(2);
        options.CustomTenantResolvers.ShouldBe(resolverTypes);
    }

    [Fact]
    public void UseCompositeResolver_WithEmptyResolverTypes_SetsEmptyCustomResolvers()
    {
        // Arrange
        var options = new MultiTenantOptions();

        // Act
        var result = options.UseCompositeResolver();

        // Assert
        result.ShouldBe(options);
        options.CustomTenantResolvers.ShouldNotBeNull();
        options.CustomTenantResolvers.ShouldBeEmpty();
    }

    [Fact]
    public void MethodChaining_ShouldWorkCorrectly()
    {
        // Arrange
        var options = new MultiTenantOptions();

        // Act
        var result = options
            .UseJwtTenantResolver()
            .UseCompositeResolver(typeof(SubdomainTenantResolver))
            .UseSubdomainTenantResolver();

        // Assert
        result.ShouldBe(options);
        options.DefaultTenantResolver.ShouldBe(typeof(SubdomainTenantResolver));
        options.CustomTenantResolvers.Length.ShouldBe(1);
        options.CustomTenantResolvers[0].ShouldBe(typeof(SubdomainTenantResolver));
    }

    [Fact]
    public void Properties_CanBeModifiedAfterConfiguration()
    {
        // Arrange
        var options = new MultiTenantOptions();

        // Act
        options.UseJwtTenantResolver()
              .UseCompositeResolver(typeof(SubdomainTenantResolver));

        options.LogViolations = false;
        options.CacheTenantResolution = false;
        options.CacheExpirationMinutes = 30;

        // Assert
        options.DefaultTenantResolver.ShouldBe(typeof(JwtTenantResolver));
        options.CustomTenantResolvers[0].ShouldBe(typeof(SubdomainTenantResolver));
        options.LogViolations.ShouldBeFalse();
        options.CacheTenantResolution.ShouldBeFalse();
        options.CacheExpirationMinutes.ShouldBe(30);
    }


    [Theory]
    [InlineData(typeof(JwtTenantResolver))]
    [InlineData(typeof(SubdomainTenantResolver))]
    [InlineData(typeof(CompositeTenantResolver))]
    public void DefaultTenantResolver_AcceptsValidResolverTypes(Type resolverType)
    {
		// Arrange
		var options = new MultiTenantOptions
		{
			// Act
			DefaultTenantResolver = resolverType
		};

		// Assert
		options.DefaultTenantResolver.ShouldBe(resolverType);
    }

    [Fact]
    public void CustomTenantResolvers_CanContainMultipleInstancesOfSameType()
    {
        // Arrange
        var options = new MultiTenantOptions();
        var resolverTypes = new[] 
        { 
            typeof(JwtTenantResolver), 
            typeof(JwtTenantResolver), 
            typeof(SubdomainTenantResolver) 
        };

        // Act
        options.UseCompositeResolver(resolverTypes);

        // Assert
        options.CustomTenantResolvers.Length.ShouldBe(3);
        options.CustomTenantResolvers[0].ShouldBe(typeof(JwtTenantResolver));
        options.CustomTenantResolvers[1].ShouldBe(typeof(JwtTenantResolver));
        options.CustomTenantResolvers[2].ShouldBe(typeof(SubdomainTenantResolver));
    }

    [Fact]
    public void ConfigurationMethods_CreateNewOptionsInstances()
    {
        // Arrange
        var options = new MultiTenantOptions();
        var jwtOptionsCalled = false;
        var subdomainOptionsCalled = false;

        // Act
        options.UseJwtTenantResolver(jwtOpts => { jwtOptionsCalled = true; });
        options.UseSubdomainTenantResolver(subdomainOpts => { subdomainOptionsCalled = true; });

        // Assert
        jwtOptionsCalled.ShouldBeTrue();
        subdomainOptionsCalled.ShouldBeTrue();
    }
}