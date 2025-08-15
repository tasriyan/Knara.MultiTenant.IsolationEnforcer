namespace Multitenant.Enforcer.Core;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class AllowCrossTenantAccessAttribute : Attribute
{
	public string Justification { get; }

	public string[] RequiredRoles { get; }

	public AllowCrossTenantAccessAttribute(string justification, params string[] requiredRoles)
	{
		Justification = justification ?? throw new ArgumentNullException(nameof(justification));
		RequiredRoles = requiredRoles ?? Array.Empty<string>();
	}
}
