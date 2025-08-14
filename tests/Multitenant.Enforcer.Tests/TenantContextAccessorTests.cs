using Multitenant.Enforcer.Core;

namespace MultiTenant.Enforcer.Tests;

    public class TenantContextAccessorTests
    {
        [Fact]
        public void Current_WhenNoContextSet_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var accessor = new TenantContextAccessor();

            // Act & Assert
            var exception = Should.Throw<InvalidOperationException>(() => accessor.Current);
            exception.Message.ShouldContain("No tenant context set");
            exception.Message.ShouldContain("TenantContextMiddleware");
        }

        [Fact]
        public void SetContext_WithValidContext_ShouldSetCurrent()
        {
            // Arrange
            var accessor = new TenantContextAccessor();
            var tenantId = Guid.NewGuid();
            var context = TenantContext.ForTenant(tenantId, "Test");

            // Act
            accessor.SetContext(context);

            // Assert
            accessor.Current.ShouldBe(context);
            accessor.Current.TenantId.ShouldBe(tenantId);
            accessor.Current.IsSystemContext.ShouldBeFalse();
            accessor.Current.ContextSource.ShouldBe("Test");
        }

        [Fact]
        public void SetContext_WithNullContext_ShouldThrowArgumentNullException()
        {
            // Arrange
            var accessor = new TenantContextAccessor();

            // Act & Assert
            var exception = Should.Throw<ArgumentNullException>(() => accessor.SetContext(null!));
            exception.ParamName.ShouldBe("context");
        }

        [Fact]
        public void SetContext_WithSystemContext_ShouldSetSystemCurrent()
        {
            // Arrange
            var accessor = new TenantContextAccessor();
            var context = TenantContext.SystemContext("BackgroundJob");

            // Act
            accessor.SetContext(context);

            // Assert
            accessor.Current.ShouldBe(context);
            accessor.Current.TenantId.ShouldBe(Guid.Empty);
            accessor.Current.IsSystemContext.ShouldBeTrue();
            accessor.Current.ContextSource.ShouldBe("BackgroundJob");
        }

        [Fact]
        public void SetContext_CalledMultipleTimes_ShouldUpdateCurrent()
        {
            // Arrange
            var accessor = new TenantContextAccessor();
            var firstContext = TenantContext.ForTenant(Guid.NewGuid(), "First");
            var secondContext = TenantContext.ForTenant(Guid.NewGuid(), "Second");

            // Act
            accessor.SetContext(firstContext);
            var firstResult = accessor.Current;
            
            accessor.SetContext(secondContext);
            var secondResult = accessor.Current;

            // Assert
            firstResult.ShouldBe(firstContext);
            secondResult.ShouldBe(secondContext);
            secondResult.ShouldNotBe(firstContext);
        }
    }