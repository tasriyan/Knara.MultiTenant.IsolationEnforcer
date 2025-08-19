namespace Multitenant.Enforcer.DomainResolvers;

public static class ValidationExtensions
{
	public static bool IsNullOrEmpty(this Guid? value)
	{
		return !value.HasValue || value.Value == Guid.Empty;
	}
}
