using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Multitenant.Enforcer.Core.RoslynAnalyzers;

public static class DiagnosticDescriptors
{
	// Error: Direct DbSet access on tenant-isolated entities
	public static readonly DiagnosticDescriptor DirectDbSetAccess = new DiagnosticDescriptor(
		"MTI001",
		"Direct DbSet access on tenant-isolated entity",
		"Use ITenantRepository<{0}> instead of direct DbSet access to ensure tenant isolation",
		"Security",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Direct access to DbSet<T> bypasses tenant isolation. Use ITenantRepository<T> instead.",
		helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI001");

	// Error: Missing AllowCrossTenantAccess attribute for cross-tenant operations
	public static readonly DiagnosticDescriptor MissingCrossTenantAttribute = new DiagnosticDescriptor(
		"MTI002",
		"Cross-tenant operation without authorization",
		"Method using ICrossTenantOperationManager must have [AllowCrossTenantAccess] attribute",
		"Security",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Cross-tenant operations require explicit authorization with [AllowCrossTenantAccess] attribute.",
		helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI002");

	// Warning: Potential tenant filter bypass
	public static readonly DiagnosticDescriptor PotentialFilterBypass = new DiagnosticDescriptor(
		"MTI003",
		"Potential tenant filter bypass detected",
		"Query on {0} might bypass tenant filtering. Verify this is intentional and properly authorized.",
		"Security",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Query patterns that might bypass tenant filtering should be carefully reviewed.",
		helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI003");

	// Warning: Tenant isolated entity without proper repository
	public static readonly DiagnosticDescriptor TenantEntityWithoutRepository = new DiagnosticDescriptor(
		"MTI004",
		"Tenant-isolated entity accessed without repository",
		"Entity {0} implements ITenantIsolated but is not accessed through ITenantRepository<T>",
		"Security",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Tenant-isolated entities should be accessed through ITenantRepository<T> for proper isolation.",
		helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI004");

	// Error: System context usage without authorization
	public static readonly DiagnosticDescriptor UnauthorizedSystemContext = new DiagnosticDescriptor(
		"MTI005",
		"Unauthorized system context usage",
		"TenantContext.SystemContext() usage requires [AllowCrossTenantAccess] attribute",
		"Security",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Creating system context requires explicit authorization.",
		helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI005");
}
