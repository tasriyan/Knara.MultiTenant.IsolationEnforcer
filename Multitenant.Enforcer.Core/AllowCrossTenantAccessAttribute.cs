namespace Multitenant.Enforcer.Core;

/// <summary>
/// Attribute for methods that legitimately need cross-tenant access.
/// Required by the analyzer for any method using ICrossTenantOperationManager.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class AllowCrossTenantAccessAttribute : Attribute
{
	/// <summary>
	/// Justification for why this operation needs cross-tenant access.
	/// </summary>
	public string Justification { get; }

	/// <summary>
	/// Required roles or claims for this cross-tenant operation.
	/// </summary>
	public string[] RequiredRoles { get; }

	/// <summary>
	/// Creates an attribute allowing cross-tenant access.
	/// </summary>
	/// <param name="justification">Business justification for cross-tenant access</param>
	/// <param name="requiredRoles">Required roles for authorization</param>
	public AllowCrossTenantAccessAttribute(string justification, params string[] requiredRoles)
	{
		Justification = justification ?? throw new ArgumentNullException(nameof(justification));
		RequiredRoles = requiredRoles ?? Array.Empty<string>();
	}
}
