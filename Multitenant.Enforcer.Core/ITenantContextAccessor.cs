namespace Multitenant.Enforcer.Core;

public interface ITenantContextAccessor
    {
        ITenantContext Current { get; }

        void SetContext(TenantContext context);
    }

public class TenantContextAccessor : ITenantContextAccessor
{
	private TenantContext? _current;

	public ITenantContext Current => _current ??
		throw new InvalidOperationException(
			"No tenant context set. Did you forget to add the TenantContextMiddleware?");

	public void SetContext(TenantContext context)
	{
		_current = context ?? throw new ArgumentNullException(nameof(context));
	}
}
