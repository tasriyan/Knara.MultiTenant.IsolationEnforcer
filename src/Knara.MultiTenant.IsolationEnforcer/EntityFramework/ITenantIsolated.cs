namespace Knara.MultiTenant.IsolationEnforcer.EntityFramework;

    public interface ITenantIsolated
    {
        Guid TenantId { get; set; }
    }
