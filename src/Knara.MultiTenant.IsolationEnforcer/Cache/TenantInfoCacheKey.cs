namespace Knara.MultiTenant.IsolationEnforcer.Cache;

public class TenantInfoCacheKey
{
	private readonly Guid _tenantId;

	public TenantInfoCacheKey(Guid tenantId)
	{
		if (tenantId == Guid.Empty)
			throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));
		_tenantId = tenantId;
	}

	public static implicit operator string(TenantInfoCacheKey key) => $"tenant_info_{key._tenantId}";

	public Guid TenantId => _tenantId;

	public override string ToString() => $"tenant_info_{_tenantId}";

	public override bool Equals(object? obj)
	{
		return obj is TenantInfoCacheKey other && _tenantId.Equals(other._tenantId);
	}

	public override int GetHashCode()
	{
		return _tenantId.GetHashCode();
	}

	public static bool operator ==(TenantInfoCacheKey? left, TenantInfoCacheKey? right)
	{
		return left?.Equals(right) ?? right is null;
	}

	public static bool operator !=(TenantInfoCacheKey? left, TenantInfoCacheKey? right)
	{
		return !(left == right);
	}
}
