using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;
using System.Reflection;

namespace Multitenant.Enforcer.EntityFramework;

/// <summary>
/// Base DbContext class that automatically enforces tenant isolation.
/// All DbContext classes should inherit from this to get tenant protection.
/// </summary>
public abstract class TenantDbContext : DbContext
{
	private readonly ITenantContextAccessor _tenantAccessor;
	private readonly ILogger<TenantDbContext> _logger;

	protected TenantDbContext(
		DbContextOptions options,
		ITenantContextAccessor tenantAccessor,
		ILogger<TenantDbContext> logger)
		: base(options)
	{
		_tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		ConfigureTenantIsolation(modelBuilder);
	}

	public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		ProcessTenantIsolatedEntities();

		try
		{
			var result = await base.SaveChangesAsync(cancellationToken);

			var tenantContext = _tenantAccessor.Current;
			_logger.LogDebug("SaveChanges completed: {Changes} changes for tenant {TenantId} (System: {IsSystem})",
				result, tenantContext.TenantId, tenantContext.IsSystemContext);

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "SaveChanges failed for tenant {TenantId}",
				_tenantAccessor.Current.TenantId);
			throw;
		}
	}

	public override int SaveChanges()
	{
		ProcessTenantIsolatedEntities();

		try
		{
			var result = base.SaveChanges();

			var tenantContext = _tenantAccessor.Current;
			_logger.LogDebug("SaveChanges completed: {Changes} changes for tenant {TenantId} (System: {IsSystem})",
				result, tenantContext.TenantId, tenantContext.IsSystemContext);

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "SaveChanges failed for tenant {TenantId}",
				_tenantAccessor.Current.TenantId);
			throw;
		}
	}

	private void ConfigureTenantIsolation(ModelBuilder modelBuilder)
	{
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			if (typeof(ITenantIsolated).IsAssignableFrom(entityType.ClrType))
			{
				_logger.LogDebug("Configuring tenant isolation for entity: {EntityType}", entityType.ClrType.Name);

				// Add tenant ID index for performance
				modelBuilder.Entity(entityType.ClrType)
						.HasIndex("IX", entityType.ClrType.Name, nameof(ITenantIsolated.TenantId));

				// Apply global query filter for tenant isolation
				var method = typeof(TenantDbContext)
					.GetMethod(nameof(SetGlobalQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
					.MakeGenericMethod(entityType.ClrType);

				method.Invoke(this, [modelBuilder]);

				// Configure TenantId as required
				modelBuilder.Entity(entityType.ClrType)
					.Property(nameof(ITenantIsolated.TenantId))
					.IsRequired();
			}
		}
	}

	private void SetGlobalQueryFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantIsolated
	{
		modelBuilder.Entity<T>().HasQueryFilter(entity =>
			_tenantAccessor.Current.IsSystemContext ||
			entity.TenantId == _tenantAccessor.Current.TenantId);

		_logger.LogDebug("Applied global query filter for {EntityType}", typeof(T).Name);
	}

	private void ProcessTenantIsolatedEntities()
	{
		var tenantContext = _tenantAccessor.Current;

		var addedEntities = ChangeTracker.Entries<ITenantIsolated>()
			.Where(e => e.State == EntityState.Added)
			.ToList();

		foreach (var entry in addedEntities)
		{
			var entity = entry.Entity;

			if (tenantContext.IsSystemContext)
			{
				if (entity.TenantId == Guid.Empty)
				{
					throw new TenantIsolationViolationException(
						$"Entity {entity.GetType().Name} in system context must have an explicit TenantId");
				}
			}
			else
			{
				if (entity.TenantId == Guid.Empty)
				{
					// Auto-assign tenant ID
					entity.TenantId = tenantContext.TenantId;
					_logger.LogDebug("Auto-assigned TenantId {TenantId} to {EntityType}",
						tenantContext.TenantId, entity.GetType().Name);
				}
				else if (entity.TenantId != tenantContext.TenantId)
				{
					// Prevent cross-tenant entity creation
					throw new TenantIsolationViolationException(
						$"Attempted to add {entity.GetType().Name} with TenantId {entity.TenantId} " +
						$"but current context is {tenantContext.TenantId}",
						tenantContext.TenantId,
						entity.TenantId,
						entity.GetType().Name,
						GetEntityId(entity));
				}
			}
		}

		// Validate modified and deleted entities
		if (!tenantContext.IsSystemContext)
		{
			var modifiedOrDeletedEntities = ChangeTracker.Entries<ITenantIsolated>()
				.Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
				.Where(e => e.Entity.TenantId != tenantContext.TenantId)
				.ToList();

			if (modifiedOrDeletedEntities.Any())
			{
				var violations = modifiedOrDeletedEntities.Select(e =>
					$"{e.Entity.GetType().Name}({e.Entity.TenantId})").ToList();

				_logger.LogCritical(
					"TENANT ISOLATION VIOLATION: Current context {CurrentTenant} attempted to modify entities from other tenants: {Violations}",
					tenantContext.TenantId, string.Join(", ", violations));

				throw new TenantIsolationViolationException(
					$"Cross-tenant modification detected. Current tenant: {tenantContext.TenantId}, " +
					$"violated entities: {string.Join(", ", violations)}",
					tenantContext.TenantId);
			}
		}
	}

	private static Guid? GetEntityId(ITenantIsolated entity)
	{
		try
		{
			var idProperty = entity.GetType().GetProperty("Id");
			if (idProperty?.PropertyType == typeof(Guid) || idProperty?.PropertyType == typeof(Guid?))
			{
				return (Guid?)idProperty.GetValue(entity);
			}
		}
		catch
		{
			// Ignore if we can't get the ID
		}
		return null;
	}
}
