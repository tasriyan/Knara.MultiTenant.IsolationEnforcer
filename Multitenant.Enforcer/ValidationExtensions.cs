namespace Multitenant.Enforcer;

public static class ValidationExtensions
{
	public static bool IsNullOrEmpty(this Guid? value)
	{
		if (!value.HasValue || value.Value == Guid.Empty)
		{
			return false;
		}
		return true;
	}
}
