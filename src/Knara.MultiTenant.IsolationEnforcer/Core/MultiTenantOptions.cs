namespace Knara.MultiTenant.IsolationEnforcer.Core;

public class MultiTenantOptions
{
	public bool CacheTenantResolution { get; set; } = true;
	public int CacheExpirationMinutes { get; set; } = 5;
	public static MultiTenantOptions DefaultOptions { get; } = new MultiTenantOptions();
}
