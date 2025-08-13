using System;

namespace MultiTenant.Enforcer.Core
{
    public interface ITenantContext
    {
        /// <summary>
        /// The ID of the current tenant.
        /// </summary>
        Guid TenantId { get; }

        /// <summary>
        /// Indicates whether this is a system context that can access data across all tenants.
        /// Only authorized operations should use system context.
        /// </summary>
        bool IsSystemContext { get; }

        /// <summary>
        /// The source of the tenant context (e.g., "JWT", "Header", "Subdomain").
        /// Used for debugging and audit purposes.
        /// </summary>
        string ContextSource { get; }
    }

    public class TenantContext : ITenantContext
    {
        public Guid TenantId { get; private set; }
        public bool IsSystemContext { get; private set; }
        public string ContextSource { get; private set; }

        private TenantContext(Guid tenantId, bool isSystemContext, string source)
        {
            TenantId = tenantId;
            IsSystemContext = isSystemContext;
            ContextSource = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Creates a tenant context for a specific tenant.
        /// </summary>
        /// <param name="tenantId">The tenant ID</param>
        /// <param name="source">The source of the tenant identification</param>
        /// <returns>A tenant context for the specified tenant</returns>
        public static TenantContext ForTenant(Guid tenantId, string source)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));

            return new TenantContext(tenantId, false, source);
        }

        /// <summary>
        /// Creates a system context that can access data across all tenants.
        /// Should only be used for authorized cross-tenant operations.
        /// </summary>
        /// <param name="source">The source of the system context</param>
        /// <returns>A system context</returns>
        public static TenantContext SystemContext(string source)
        {
            return new TenantContext(Guid.Empty, true, source);
        }
    }
}
