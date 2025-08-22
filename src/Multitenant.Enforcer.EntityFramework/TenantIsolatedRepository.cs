using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;
using System.Linq.Expressions;

namespace MultiTenant.Enforcer.EntityFramework;

public class TenantIsolatedRepository<T, TContext>(
	TContext context,
	ITenantContextAccessor tenantAccessor,
	ILogger<TenantIsolatedRepository<T, TContext>> logger) : ITenantIsolatedRepository<T, TContext>
	where T : class, ITenantIsolated
	where TContext : DbContext
{
	protected readonly TContext _context = context ?? throw new ArgumentNullException(nameof(context));
	protected readonly ITenantContextAccessor _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));

	public TContext Context => _context;

	public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return await Query().FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id, cancellationToken);
	}

	public virtual async Task<List<T>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
	{
		var idList = ids.ToList();
		return await Query().Where(e => idList.Contains(EF.Property<Guid>(e, "Id"))).ToListAsync(cancellationToken);
	}

	public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return await Query().ToListAsync(cancellationToken);
	}

	public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
	{
		return await Query().Where(predicate).ToListAsync(cancellationToken);
	}

	public virtual async Task<T?> FindSingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
	{
		return await Query().FirstOrDefaultAsync(predicate, cancellationToken);
	}

	public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
	{
		return await Query().AnyAsync(predicate, cancellationToken);
	}

	public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
	{
		var query = Query();
		return predicate == null
			? await query.CountAsync(cancellationToken)
			: await query.CountAsync(predicate, cancellationToken);
	}

	public virtual IQueryable<T> Query()
	{
		var tenantContext = _tenantAccessor.Current;
		var query = _context.Set<T>().AsQueryable();

		// MANUAL tenant filtering - don't rely on global filters that may not exist
		if (!tenantContext.IsSystemContext)
		{
			query = query.Where(e => e.TenantId == tenantContext.TenantId);
		}

		logger.LogDebug("Creating tenant-filtered query for {EntityType} with tenant context {TenantId} (System: {IsSystem})",
			typeof(T).Name, tenantContext.TenantId, tenantContext.IsSystemContext);

		return query;
	}

	public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entity);

		ValidateAndSetTenantId(entity);

		_context.Set<T>().Add(entity);
		await SaveChangesAsync(cancellationToken);

		logger.LogInformation("Added {EntityType} with ID {EntityId} for tenant {TenantId}",
			typeof(T).Name, GetEntityId(entity), entity.TenantId);

		return entity;
	}

	public virtual async Task<List<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entities);

		var entityList = entities.ToList();
		foreach (var entity in entityList)
		{
			ValidateAndSetTenantId(entity);
		}

		_context.Set<T>().AddRange(entityList);
		await SaveChangesAsync(cancellationToken);

		logger.LogInformation("Added {Count} {EntityType} entities for tenant {TenantId}",
			entityList.Count, typeof(T).Name, _tenantAccessor.Current.TenantId);

		return entityList;
	}

	public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entity);

		// CRITICAL: Validate that we own this entity BEFORE updating
		await ValidateTenantOwnershipAsync(entity, cancellationToken);

		_context.Set<T>().Update(entity);
		await SaveChangesAsync(cancellationToken);

		logger.LogInformation("Updated {EntityType} with ID {EntityId} for tenant {TenantId}",
				typeof(T).Name, GetEntityId(entity), entity.TenantId);

		return entity;
	}

	public virtual async Task<List<T>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entities);

		var entityList = entities.ToList();
		
		// CRITICAL: Validate ALL entities before updating ANY
		foreach (var entity in entityList)
		{
			await ValidateTenantOwnershipAsync(entity, cancellationToken);
		}

		_context.Set<T>().UpdateRange(entityList);
		await SaveChangesAsync(cancellationToken);

		logger.LogInformation("Updated {Count} {EntityType} entities for tenant {TenantId}",
				entityList.Count, typeof(T).Name, _tenantAccessor.Current.TenantId);

		return entityList;
	}

	public virtual async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		// Use tenant-filtered query to get entity - if not found, it doesn't belong to us
		var entity = await Query().FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id, cancellationToken);
		if (entity == null)
			return false;

		await DeleteAsync(entity, cancellationToken);
		return true;
	}

	public virtual async Task<T> DeleteAsync(T entity, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entity);

		// CRITICAL: Validate that we own this entity BEFORE deleting
		await ValidateTenantOwnershipAsync(entity, cancellationToken);

		_context.Set<T>().Remove(entity);
		await SaveChangesAsync(cancellationToken);

		logger.LogInformation("Deleted {EntityType} with ID {EntityId} for tenant {TenantId}",
				typeof(T).Name, GetEntityId(entity), entity.TenantId);

		return entity;
	}

	public virtual async Task<List<T>> DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entities);

		var entityList = entities.ToList();
		
		// CRITICAL: Validate ALL entities before deleting ANY
		foreach (var entity in entityList)
		{
			await ValidateTenantOwnershipAsync(entity, cancellationToken);
		}

		_context.Set<T>().RemoveRange(entityList);
		await SaveChangesAsync(cancellationToken);

		logger.LogInformation("Deleted {Count} {EntityType} entities for tenant {TenantId}",
				entityList.Count, typeof(T).Name, _tenantAccessor.Current.TenantId);

		return entityList;
	}

	public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		return await _context.SaveChangesAsync(cancellationToken);
	}

	protected virtual void ValidateAndSetTenantId(T entity)
	{
		var tenantContext = _tenantAccessor.Current;

		if (tenantContext.IsSystemContext)
		{
			// System context can work with any tenant, but entity must have a valid TenantId
			if (entity.TenantId == Guid.Empty)
			{
				throw new TenantIsolationViolationException(
					$"Entity {typeof(T).Name} in system context must have a valid TenantId");
			}
			return;
		}

		if (entity.TenantId == Guid.Empty)
		{
			// Auto-assign tenant ID
			entity.TenantId = tenantContext.TenantId;
			logger.LogDebug("Auto-assigned TenantId {TenantId} to {EntityType}",
					tenantContext.TenantId, typeof(T).Name);
		}
		else if (entity.TenantId != tenantContext.TenantId)
		{
			throw new TenantIsolationViolationException(
				$"Attempted to add {typeof(T).Name} with TenantId {entity.TenantId} " +
				$"but current context is {tenantContext.TenantId}",
				tenantContext.TenantId,
				entity.TenantId,
				typeof(T).Name,
				GetEntityId(entity));
		}
	}

	protected virtual async Task ValidateTenantOwnershipAsync(T entity, CancellationToken cancellationToken = default)
	{
		var tenantContext = _tenantAccessor.Current;

		if (tenantContext.IsSystemContext)
			return; // System context can modify any tenant's data

		// CRITICAL: For updates/deletes, we need to verify the entity actually exists in our tenant
		// Don't trust the TenantId on the incoming entity - verify it exists in our filtered query
		var entityId = GetEntityId(entity);
		if (entityId.HasValue)
		{
			var exists = await Query().AnyAsync(e => EF.Property<Guid>(e, "Id") == entityId.Value, cancellationToken);
			if (!exists)
			{
				throw new TenantIsolationViolationException(
					$"Entity {typeof(T).Name} with ID {entityId} does not exist or does not belong to tenant {tenantContext.TenantId}",
					tenantContext.TenantId,
					entity.TenantId,
					typeof(T).Name,
					entityId);
			}
		}
		else
		{
			// Fallback to basic tenant ID check if we can't get entity ID
			if (entity.TenantId != tenantContext.TenantId)
			{
				throw new TenantIsolationViolationException(
					$"Cross-tenant modification detected. Current tenant: {tenantContext.TenantId}, " +
					$"entity tenant: {entity.TenantId}",
					tenantContext.TenantId,
					entity.TenantId,
					typeof(T).Name,
					GetEntityId(entity));
			}
		}
	}

	protected virtual Guid? GetEntityId(T entity)
	{
		try
		{
			var idProperty = typeof(T).GetProperty("Id");
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
