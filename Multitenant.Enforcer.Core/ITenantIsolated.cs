using System;

namespace MultiTenant.Enforcer.Core
{
    /// <summary>
    /// Interface that marks an entity as requiring tenant isolation.
    /// All entities implementing this interface will be automatically protected by the enforcer.
    /// </summary>
    public interface ITenantIsolated
    {
        /// <summary>
        /// The tenant ID that this entity belongs to.
        /// This property is used for automatic tenant filtering and validation.
        /// </summary>
        Guid TenantId { get; set; }
    }

    /// <summary>
    /// Interface for entities that may need legitimate cross-tenant access in specific scenarios.
    /// These entities still require tenant isolation but can be accessed across tenants
    /// when using proper authorization attributes.
    /// </summary>
    public interface ICrossTenantAccessible : ITenantIsolated
    {
        // Marker interface - no additional properties required
        // Analyzer will require explicit authorization for cross-tenant operations
    }
}
