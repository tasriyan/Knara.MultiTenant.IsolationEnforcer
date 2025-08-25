using Multitenant.Enforcer.EntityFramework;
using System.Linq.Expressions;

namespace MultiTenant.Enforcer.EntityFramework;

    public interface ITenantIsolatedRepository<T> where T : class, ITenantIsolated
    {
        Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<T>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
        Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
        Task<T?> FindSingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
        Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
        IQueryable<T> Query();
        Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
        Task<List<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
        Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
        Task<List<T>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<T> DeleteAsync(T entity, CancellationToken cancellationToken = default);
        Task<List<T>> DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    }

    public interface ITenantIsolatedRepository<T, TContext> : ITenantIsolatedRepository<T> 
        where T : class, ITenantIsolated
        where TContext : class
    {
        TContext Context { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
