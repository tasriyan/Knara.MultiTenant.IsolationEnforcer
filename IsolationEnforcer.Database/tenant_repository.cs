// MultiTenant.Enforcer.EntityFramework/ITenantRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Threading;
using MultiTenant.Enforcer.Core;

namespace MultiTenant.Enforcer.EntityFramework
{
    /// <summary>
    /// Interface for tenant-aware repository operations.
    /// All operations automatically apply tenant isolation.
    /// </summary>
    /// <typeparam name="T">The entity type that implements ITenantIsolated</typeparam>
    public interface ITenantRepository<T> where T : class, ITenantIsolated
    {
        /// <summary>
        /// Gets an entity by its ID. Automatically filtered to current tenant.
        /// </summary>
        /// <param name="id">The entity ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The entity if found and belongs to current tenant, null otherwise</returns>
        Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets multiple entities by their IDs. Automatically filtered to current tenant.
        /// </summary>
        /// <param name="ids">The entity IDs</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Entities that exist and belong to current tenant</returns>
        Task<List<T>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all entities for the current tenant.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>All entities belonging to current tenant</returns>
        Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds entities matching the specified predicate. Automatically filtered to current tenant.
        /// </summary>
        /// <param name="predicate">The search predicate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Entities matching the predicate and belonging to current tenant</returns>
        Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a single entity matching the specified predicate. Automatically filtered to current tenant.
        /// </summary>
        /// <param name="predicate">The search predicate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The entity if found and belongs to current tenant, null otherwise</returns>
        Task<T?> FindSingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if any entities match the specified predicate. Automatically filtered to current tenant.
        /// </summary>
        /// <param name="predicate">The search predicate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if any entities match and belong to current tenant</returns>
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts entities matching the specified predicate. Automatically filtered to current tenant.
        /// </summary>
        /// <param name="predicate">The count predicate (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Count of entities belonging to current tenant</returns>
        Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a queryable for the entity type. Automatically filtered to current tenant.
        /// Use this for complex LINQ operations.
        /// </summary>
        /// <returns>IQueryable automatically filtered to current tenant</returns>
        IQueryable<T> Query();

        /// <summary>
        /// Adds a new entity. TenantId is automatically set to current tenant.
        /// </summary>
        /// <param name="entity">The entity to add</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The added entity</returns>
        Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds multiple entities. TenantId is automatically set to current tenant for all entities.
        /// </summary>
        /// <param name="entities">The entities to add</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The added entities</returns>
        Task<List<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing entity. Validates that entity belongs to current tenant.
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated entity</returns>
        Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates multiple entities. Validates that all entities belong to current tenant.
        /// </summary>
        /// <param name="entities">The entities to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated entities</returns>
        Task<List<T>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an entity by ID. Validates that entity belongs to current tenant.
        /// </summary>
        /// <param name="id">The entity ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if entity was found and deleted</returns>
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an entity. Validates that entity belongs to current tenant.
        /// </summary>
        /// <param name="entity">The entity to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deleted entity</returns>
        Task<T> DeleteAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes multiple entities. Validates that all entities belong to current tenant.
        /// </summary>
        /// <param name="entities">The entities to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The deleted entities</returns>
        Task<List<T>> DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a bulk update operation. Automatically filtered to current tenant.
        /// </summary>
        /// <param name="filter">Filter for entities to update</param>
        /// <param name="updateExpression">Update expression</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of entities updated</returns>
        Task<int> BulkUpdateAsync(
            Expression<Func<T, bool>> filter,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a bulk delete operation. Automatically filtered to current tenant.
        /// </summary>
        /// <param name="filter">Filter for entities to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of entities deleted</returns>
        Task<int> BulkDeleteAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for tenant-aware repository operations that require additional context (like DbContext access).
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TContext">The DbContext type</typeparam>
    public interface ITenantRepository<T, TContext> : ITenantRepository<T> 
        where T : class, ITenantIsolated
        where TContext : class
    {
        /// <summary>
        /// Gets the underlying DbContext for advanced operations.
        /// Use with caution - ensures proper tenant filtering is applied.
        /// </summary>
        TContext Context { get; }

        /// <summary>
        /// Saves all changes to the context with tenant validation.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of entities saved</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
