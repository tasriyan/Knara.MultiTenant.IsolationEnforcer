namespace Multitenant.Enforcer.EntityFramework;

    public interface ITenantIsolated
    {
        Guid TenantId { get; set; }
    }
