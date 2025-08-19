using Microsoft.AspNetCore.Http;

namespace Multitenant.Enforcer.DomainResolvers;

public static class HttpContextExtensions
{
	// Assuming subdomain-based tenancy
	// Patterns:
	//		https://acme-corp.yourapp.com
	//		https://www.globex.yourapp.com  
	//		https://admin.initech.yourapp.com
	//		https://yourapp.com (no subdomain)
	//		https://localhost:5000 (no subdomain)
	public static string ExtractSubdomainFromDomain(this HttpContext context, string[] excludedSubdomains)
	{
		var host = context.Request.Host.Host;
		var parts = host.Split('.');

		// Need at least 3 parts for subdomain: subdomain.domain.com
		if (parts.Length < 3) return string.Empty;

		// Check if first part should be skipped (www, admin, etc.)
		if (excludedSubdomains?.Contains(parts[0], StringComparer.OrdinalIgnoreCase) == true)
		{
			// Use second part as tenant: www.globex.yourapp.com -> "globex"
			return parts.Length >= 3 ? parts[1] : string.Empty;
		}

		// Use first part as tenant: acme-corp.yourapp.com -> "acme-corp"
		return parts[0];
	}

	// Assuming query parameter-based tenancy
	// Patterns:
	//		https://yourapp.com?tenant=acme-corp
	//		https://yourapp.com?subdomain=globex
	//		https://yourapp.com?tenant=admin
	//		https://yourapp.com?tenantId=11111111-1111-1111-1111-111111111111
	public static string ExtractSubdomaintFromQuery(this HttpContext context, string[] includedQueryParameters)
	{
		// Try query parameter
		foreach (var header in includedQueryParameters)
		{
			if (context.Request.Query.TryGetValue(header, out var queryValue))
			{
				return queryValue.FirstOrDefault();
			}
		}
		return null;
	}

	// Assuming header-based tenancy
	// Patterns:
	//		X-Tenant: acme-corp
	//		X-Tenant-Subdomain: globex
	//		X-Tenant: admin
	//		X-Tenant-Id: 11111111-1111-1111-1111-111111111111
	public static string? ExtractSubdomainFromHeader(this HttpContext context, string[] includedHeaders)
	{
		foreach (var header in includedHeaders)
		{
			if (context.Request.Headers.TryGetValue(header, out var headerValue))
			{
				return headerValue.FirstOrDefault();
			}
		}
		return null;
	}

	// Assuming path-based tenancy
	// Patterns:
	//		https://yourapp.com/acme-corp/dashboard
	//		https://yourapp.com/globex/reports
	//		https://yourapp.com/admin
	//		https://yourapp.com/tenant/initech/settings
	//		https://yourapp.com/api/v1/globex/users
	public static string? ExtractSubdomainFromPath(this HttpContext context, string[] excludedPathSegments)
	{
		var pathSegments = context.Request.Path.Value?.Split(['/'], options: StringSplitOptions.RemoveEmptyEntries);
		if (pathSegments == null || pathSegments.Length == 0) 
			return null;
		foreach (var segment in pathSegments)
		{
			if (excludedPathSegments.Contains(segment, StringComparer.OrdinalIgnoreCase))
				continue;
			else
				return segment; // Return the first non-excluded segment as subdomain
		}
		return null;
	}
}
