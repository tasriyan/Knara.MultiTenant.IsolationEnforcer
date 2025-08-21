namespace Multitenant.Enforcer.Core;

    public interface ITenantIsolated
    {
        Guid TenantId { get; set; }
    }
