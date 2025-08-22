using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.EntityFramework;

namespace MultiTenantEnforcer.IntegrationTests;

public class TenantIsolatedDbContext(DbContextOptions<TenantIsolatedDbContext> options, 
	ITenantContextAccessor tenantAccessor, 
	ILogger<TenantIsolatedDbContext> logger) : TenantDbContext(options, tenantAccessor, logger)
{
	public DbSet<TestEntity> TestEntities { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<TestEntity>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.HasIndex(e => new { e.TenantId, e.Name });
		});
	}
}

public class UnsafeTestDbContext(DbContextOptions<UnsafeTestDbContext> options) : DbContext(options)
{
	public DbSet<TestEntity> TestEntities { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<TestEntity>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.HasIndex(e => new { e.TenantId, e.Name });
		});
	}
}

public class TestTenantsStoreDbContext(DbContextOptions<TestTenantsStoreDbContext> options) : DbContext(options)
{
	public DbSet<TestTenant> Tenants { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<TestTenant>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Domain).IsRequired().HasMaxLength(100);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.HasIndex(e => e.Domain).IsUnique();
		});
	}
}

public class TestTenant
{
	public Guid Id { get; set; }
	public string Domain { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
}

public class TestTenantStore(TestTenantsStoreDbContext context) : IReadOnlyTenants
{
	public async Task<TenantInfo[]> GetAllActiveTenantsAsync(CancellationToken cancellationToken)
	{
		return await context.Tenants
			.Where(t => t.IsActive)
			.Select(t => new TenantInfo
			{
				Id = t.Id,
				IsActive = t.IsActive
			})
			.ToArrayAsync(cancellationToken);
	}

	public async Task<TenantInfo?> GetTenantInfoByDomainAsync(string domain, CancellationToken cancellationToken = default)
	{
		var tenant = await context.Tenants
			.Where(t => t.Domain == domain && t.IsActive)
			.FirstOrDefaultAsync(cancellationToken);

		return tenant == null ? null : new TenantInfo
		{
			Id = tenant.Id,
			IsActive = tenant.IsActive
		};
	}

	public async Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId, CancellationToken cancellationToken = default)
	{
		var tenant = await context.Tenants
			.Where(t => t.Id == tenantId && t.IsActive)
			.FirstOrDefaultAsync(cancellationToken);

		return tenant == null ? null : new TenantInfo
		{
			Id = tenant.Id,
			IsActive = tenant.IsActive
		};
	}
}

public class TestEntity : ITenantIsolated
{
	public Guid Id { get; set; }
	public Guid TenantId { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
}
