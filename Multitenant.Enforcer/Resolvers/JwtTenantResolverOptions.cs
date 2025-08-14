namespace Multitenant.Enforcer.Resolvers;

/// <summary>
/// JWT tenant resolver configuration.
/// </summary>
public class JwtTenantResolverOptions
{
	/// <summary>
	/// The claim type that contains the tenant ID.
	/// </summary>
	public string TenantIdClaimType { get; set; } = "tenant_id";

	/// <summary>
	/// The claim type that indicates system admin access.
	/// </summary>
	public string SystemAdminClaimType { get; set; } = "role";

	/// <summary>
	/// The claim value that indicates system admin access.
	/// </summary>
	public string SystemAdminClaimValue { get; set; } = "SystemAdmin";
}
