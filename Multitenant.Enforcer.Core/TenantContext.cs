namespace Multitenant.Enforcer.Core;

public interface ITenantContext
{
	Guid TenantId { get; }

	bool IsSystemContext { get; }

	string ContextSource { get; }
}

public class TenantContext : ITenantContext
{
	public Guid TenantId { get; private set; }
	public bool IsSystemContext { get; private set; }
	public string ContextSource { get; private set; }


	public const string DefaultSystemContextSource = "SystemAdmin-JWT";
	private TenantContext(Guid tenantId, bool isSystemContext, string source)
	{
		TenantId = tenantId;
		IsSystemContext = isSystemContext;
		ContextSource = source ?? throw new ArgumentNullException(nameof(source));
	}

	public static TenantContext ForTenant(Guid tenantId, string source)
	{
		if (tenantId == Guid.Empty)
			throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));

		return new TenantContext(tenantId, false, source);
	}

	public static TenantContext SystemContext(string source = DefaultSystemContextSource)
	{
		return new TenantContext(Guid.Empty, true, source);
	}
}
