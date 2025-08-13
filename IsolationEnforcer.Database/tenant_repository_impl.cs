// MultiTenant.Enforcer.EntityFramework/TenantRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MultiTenant.Enforcer.Core;
using MultiTenant.Enforcer.Core.Exceptions;

namespace MultiTenant.Enforcer.EntityFramework
{
    /// <summary>
    /// Base implementation of tenant-aware repository with automatic tenant isolation.
    /// </summary>
    /// <typeparam name="T">The entity type that implements ITenantIsolated</typeparam>
    /// <typeparam name="TContext">The DbContext type</typeparam>
    public class TenantRepository<T, TContext> : ITenantRepository<T, TContext>
        where T : class, ITenantIsolated
        where TContext : DbContext
    {
        protected readonly TContext _context;
        protected readonly ITenantContextAccessor _tenantAccessor;
        protected readonly ILogger<TenantRepository<T, TContext>> _logger;

        public TContext Context => _context;

        public TenantRepository(
            TContext context,
            ITenantContextAccessor tenantAccessor,
            ILogger<TenantRepository<T, TContext>> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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
            // The global query filter in TenantDbContext will automatically apply tenant filtering
            var query = _context.Set<T>().AsQueryable();

            // Log query for performance monitoring
            var tenantContext = _tenantAccessor.Current;
            _logger.LogDebug("Creating query for {EntityType} with tenant context {TenantId} (System: {IsSystem})",
                typeof(T).Name, tenantContext.TenantId, tenantContext.IsSystemContext);

            return query;
        }

        public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            ValidateAndSetTenantId(entity);

            _context.Set<T>().Add(entity);
            await SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Added {EntityType} with ID {EntityId} for tenant {TenantId}",
                typeof(T).Name, GetEntityId(entity), entity.TenantId);

            return entity;
        }

        public virtual async Task<List<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                ValidateAndSetTenantId(entity);
            }

            _context.Set<T>().AddRange(entityList);
            await SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Added {Count} {EntityType} entities for tenant {TenantId}",
                entityList.Count, typeof(T).Name, _tenantAccessor.Current.TenantId);

            return entityList;
        }

        public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            ValidateTenantOwnership(entity);

            _context.Set<T>().Update(entity);
            await SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated {EntityType} with ID {EntityId} for tenant {TenantId}",
                typeof(T).Name, GetEntityId(entity), entity.TenantId);

            return entity;
        }

        public virtual async Task<List<T>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                ValidateTenantOwnership(entity);
            }

            _context.Set<T>().UpdateRange(entityList);
            await SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated {Count} {EntityType} entities for tenant {TenantId}",
                entityList.Count, typeof(T).Name, _tenantAccessor.Current.TenantId);

            return entityList;
        }

        public virtual async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return false;

            await DeleteAsync(entity, cancellationToken);
            return true;
        }

        public virtual async Task<T> DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            ValidateTenantOwnership(entity);

            _context.Set<T>().Remove(entity);
            await SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted {EntityType} with ID {EntityId} for tenant {TenantId}",
                typeof(T).Name, GetEntityId(entity), entity.TenantId);

            return entity;
        }

        public virtual async Task<List<T>> DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                ValidateTenantOwnership(entity);
            }

            _context.Set<T>().RemoveRange(entityList);
            await SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} {EntityType} entities for tenant {TenantId}",
                entityList.Count, typeof(T).Name, _tenantAccessor.Current.TenantId);

            return entityList;
        }

        public virtual async Task<int> BulkUpdateAsync(
            Expression<Func<T, bool>> filter,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken = default)
        {
            // Global query filter automatically applies tenant filtering
            var affectedRows = await Query()
                .Where(filter)
                .ExecuteUpdateAsync(updateExpression, cancellationToken);

            _logger.LogInformation("Bulk updated {Count} {EntityType} entities for tenant {TenantId}",
                affectedRows, typeof(T).Name, _tenantAccessor.Current.TenantId);

            return affectedRows;
        }

        public virtual async Task<int> BulkDeleteAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        {
            // Global query filter automatically applies tenant filtering
            var affectedRows = await Query()
                .Where(filter)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation("Bulk deleted {Count} {EntityType} entities for tenant {TenantId}",
                affectedRows, typeof(T).Name, _tenantAccessor.Current.TenantId);

            return affectedRows;
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
                _logger.LogDebug("Auto-assigned TenantId {TenantId} to {EntityType}",
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

        protected virtual void ValidateTenantOwnership(T entity)
        {
            var tenantContext = _tenantAccessor.Current;

            if (tenantContext.IsSystemContext)
                return; // System context can modify any tenant's data

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

    /// <summary>
    /// Generic tenant repository implementation that infers the DbContext type.
    /// </summary>
    /// <typeparam name="T">The entity type that implements ITenantIsolated</typeparam>
    public class TenantRepository<T> : TenantRepository<T, DbContext>, ITenantRepository<T>
        where T : class, ITenantIsolated
    {
        public TenantRepository(
            DbContext context,
            ITenantContextAccessor tenantAccessor,
            ILogger<TenantRepository<T, DbContext>> logger)
            : base(context, tenantAccessor, logger)
        {
        }
    }
}
