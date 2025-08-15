namespace Multitenant.Enforcer.Core;

    public interface ITenantIsolated
    {
        Guid TenantId { get; set; }
    }

    public interface ICrossTenantAccessible : ITenantIsolated
    {
        // Marker interface - no additional properties required
        // Analyzer will require explicit authorization for cross-tenant operations
    }
