namespace Multitenant.Enforcer.TenantResolvers;

public static class Validators
{
	public static bool IsNullOrEmpty(this Guid? value)
	{
		return !value.HasValue || value.Value == Guid.Empty;
	}

	public static bool IsValidTenantIdentifier(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return false;

		// Validate against injection attacks
		if (value!.Contains("../") || value.Contains("..\\")) return false;
		if (value.Any(c => char.IsControl(c))) return false;

		return value.Length <= 100; // Reasonable limit
	}
}
