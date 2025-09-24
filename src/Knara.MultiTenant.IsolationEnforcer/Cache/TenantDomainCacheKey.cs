namespace Knara.MultiTenant.IsolationEnforcer.Cache;

public class TenantDomainCacheKey
{
	private readonly string _domain;

	public TenantDomainCacheKey(string domain)
	{
		if (string.IsNullOrWhiteSpace(domain))
			throw new ArgumentException("Domain cannot be empty", nameof(_domain));
		_domain = domain;
	}

	public static implicit operator string(TenantDomainCacheKey key) => $"tenant_domain_{key._domain.ToLowerInvariant()}";

	public string Domain => _domain;

	public override string ToString() => $"tenant_info_{_domain}";

	public override bool Equals(object? obj)
	{
		return obj is TenantDomainCacheKey other && _domain.Equals(other._domain);
	}

	public override int GetHashCode()
	{
		return _domain.GetHashCode();
	}

	public static bool operator ==(TenantDomainCacheKey? left, TenantDomainCacheKey? right)
	{
		return left?.Equals(right) ?? right is null;
	}

	public static bool operator !=(TenantDomainCacheKey? left, TenantDomainCacheKey? right)
	{
		return !(left == right);
	}
}
