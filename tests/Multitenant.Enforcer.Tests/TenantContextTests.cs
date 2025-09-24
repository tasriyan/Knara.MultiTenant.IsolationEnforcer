using Knara.MultiTenant.IsolationEnforcer.Core;

namespace MultiTenant.Enforcer.Tests;

    public class TenantContextTests
    {
        [Fact]
        public void ForTenant_WithValidTenantId_ShouldCreateTenantContext()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var source = "JWT";

            // Act
            var context = TenantContext.ForTenant(tenantId, source);

            // Assert
            context.TenantId.ShouldBe(tenantId);
            context.IsSystemContext.ShouldBeFalse();
            context.ContextSource.ShouldBe(source);
        }

        [Fact]
        public void ForTenant_WithEmptyGuid_ShouldThrowArgumentException()
        {
            // Arrange
            var emptyTenantId = Guid.Empty;
            var source = "JWT";

            // Act & Assert
            var exception = Should.Throw<ArgumentException>(() => TenantContext.ForTenant(emptyTenantId, source));
            exception.ParamName.ShouldBe("tenantId");
            exception.Message.ShouldContain("Tenant ID cannot be empty");
        }

        [Fact]
        public void ForTenant_WithNullSource_ShouldThrowArgumentNullException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();

            // Act & Assert
            var exception = Should.Throw<ArgumentNullException>(() => TenantContext.ForTenant(tenantId, null!));
            exception.ParamName.ShouldBe("source");
        }

        [Fact]
        public void SystemContext_WithValidSource_ShouldCreateSystemContext()
        {
            // Arrange
            var source = "BackgroundJob";

            // Act
            var context = TenantContext.SystemContext(source);

            // Assert
            context.TenantId.ShouldBe(Guid.Empty);
            context.IsSystemContext.ShouldBeTrue();
            context.ContextSource.ShouldBe(source);
        }

        [Fact]
        public void SystemContext_WithNullSource_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Should.Throw<ArgumentNullException>(() => TenantContext.SystemContext(null!));
            exception.ParamName.ShouldBe("source");
        }

        [Theory]
        [InlineData("JWT")]
        [InlineData("Header")]
        [InlineData("Subdomain")]
        [InlineData("Test")]
        public void ForTenant_WithDifferentSources_ShouldPreserveSource(string source)
        {
            // Arrange
            var tenantId = Guid.NewGuid();

            // Act
            var context = TenantContext.ForTenant(tenantId, source);

            // Assert
            context.ContextSource.ShouldBe(source);
        }
    }