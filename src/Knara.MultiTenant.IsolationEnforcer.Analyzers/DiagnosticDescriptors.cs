using Microsoft.CodeAnalysis;

namespace Knara.MultiTenant.IsolationEnforcer.Analyzers;

public static class DiagnosticDescriptors
{
	// Error: Direct DbSet access on tenant-isolated entities
	public static readonly DiagnosticDescriptor DirectDbSetAccess = new(
		"MTI001",
		"Direct DbSet access on tenant-isolated entity",
		"Use ITenantIsolatedRepository<{0}> instead of direct DbSet access to ensure tenant isolation",
		"Security",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Direct access to DbSet<T> bypasses tenant isolation. Use ITenantIsolatedRepository<T> instead.",
		helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI001",
		customTags: [WellKnownDiagnosticTags.Telemetry]);

	// Error: Missing AllowCrossTenantAccess attribute for cross-tenant operations
	public static readonly DiagnosticDescriptor MissingCrossTenantAttribute = new(
		"MTI002",
		"Cross-tenant operation without authorization",
		"Method using ICrossTenantOperationManager must have [AllowCrossTenantAccess] attribute",
		"Security",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Cross-tenant operations require explicit authorization with [AllowCrossTenantAccess] attribute.",
		helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI002",
		customTags: [WellKnownDiagnosticTags.Telemetry]);

	// Warning: Potential tenant filter bypass
	public static readonly DiagnosticDescriptor PotentialFilterBypass = new(
		"MTI003",
		"Potential tenant filter bypass detected",
		"Query on {0} might bypass tenant filtering. Verify this is intentional and properly authorized.",
		"Security",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Query patterns that might bypass tenant filtering should be carefully reviewed.",
		helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI003",
		customTags: [WellKnownDiagnosticTags.Telemetry]);

	// Error: System context usage without authorization
	public static readonly DiagnosticDescriptor UnauthorizedSystemContext = new(
		"MTI005",
		"Unauthorized system context usage",
		"TenantContext.SystemContext() usage requires [AllowCrossTenantAccess] attribute",
		"Security",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Creating system context requires explicit authorization.",
		helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI005",
		customTags: [WellKnownDiagnosticTags.Telemetry]);
}
