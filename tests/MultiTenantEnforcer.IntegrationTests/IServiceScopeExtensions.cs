using Knara.MultiTenant.IsolationEnforcer.Core;
using Knara.MultiTenant.IsolationEnforcer.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace MultiTenantEnforcer.IntegrationTests;

public static class IServiceScopeExtensions
{
	public static UnsafeTestDbContext GetDbContext(this IServiceScope scope)
	{
		return scope.ServiceProvider.GetRequiredService<UnsafeTestDbContext>();
	}

	public static ITenantIsolatedRepository<TestEntity> GetRepository(this IServiceScope scope)
	{
		return scope.ServiceProvider.GetRequiredService<ITenantIsolatedRepository<TestEntity>>();
	}

	public static ITenantContextAccessor GetTenantAccessor(this IServiceScope scope)
	{
		return scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
	}

	public static void SetTenantContext(this IServiceScope scope, Guid tenantId, string source = "Test")
	{
		var tenantAccessor = GetTenantAccessor(scope);
		tenantAccessor.SetContext(TenantContext.ForTenant(tenantId, source));
	}

	public static void SetSystemContext(this IServiceScope scope, string source = "SystemTest")
	{
		var tenantAccessor = GetTenantAccessor(scope);
		tenantAccessor.SetContext(TenantContext.SystemContext(source));
	}
	public static TenantIsolatedDbContext GetTenantDbContext(this IServiceScope scope)
	{
		return scope.ServiceProvider.GetRequiredService<TenantIsolatedDbContext>();
	}
}