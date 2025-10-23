# Tenant Resolvers Guide

Tenant resolvers determine which tenant a request belongs to. The library includes built-in resolvers covering common multi-tenant patterns. Each implements `ITenantResolver` and can be used individually or combined.

## Subdomain Resolver

Extracts tenant from subdomain: `tenant1.yourapp.com` becomes `tenant1`

```csharp
services.AddMultiTenantIsolation()
    .WithSubdomainResolutionStrategy(options =>
    {
        options.ExcludedSubdomains = new[] { "www", "api", "admin" };
        options.SystemAdminClaimValue = "SystemAdmin";
    });
```

**Process:**
1. Check for system admin claims (returns system context if found)
2. Extract subdomain from `HttpContext.Request.Host.Host`
3. Skip excluded subdomains
4. Look up tenant via `ITenantLookupService.GetTenantInfoByDomainAsync()`
5. Throw `TenantResolutionException` if subdomain missing or tenant not found

**Use cases:**
- SaaS applications with customer-specific subdomains
- Multi-tenant applications with domain separation
- Controlled DNS with wildcard subdomain support

**Limitations:**
- Requires wildcard DNS configuration
- Local development needs host file modifications
- Limited to subdomain naming constraints

## Header Resolver

Extracts tenant from HTTP headers or query parameters:

```csharp
services.AddMultiTenantIsolation()
    .WithHeaderResolutionStrategy(options =>
    {
        options.IncludedHeaders = new[] { "X-Tenant-ID", "Tenant" };
        options.IncludedQueryParameters = new[] { "tenant", "tenantId" };
    });
```

**Process:**
1. Check for system admin claims
2. Search configured headers for tenant identifier
3. Fall back to query parameters if headers not found
4. Accept GUID tenant IDs or domain names
5. Resolve using appropriate lookup method

**Use cases:**
- API clients setting custom headers
- Testing and development
- Integration with external systems providing tenant context
- Mobile apps controlling request headers

**Limitations:**
- Requires client cooperation for correct headers
- Query parameters visible in URLs and logs
- Headers easily modified by clients

## JWT Resolver

Extracts tenant from JWT token claims with flexible formats and domain validation:

```csharp
services.AddMultiTenantIsolation()
    .WithJwtResolutionStrategy(options =>
    {
        options.TenantIdClaimTypes = new[] { "tenant_id", "allowed_tenants", "tid" };
        options.SystemAdminClaimValue = "SystemAdmin";
        options.DomainValidationMode = TenantDomainValidationMode.ValidateAgainstSubdomain;
    });
```

**Process:**
1. Check for system admin claims in JWT (returns system context if found)
2. Search configured claim types for tenant information
3. Support multiple tenant formats:
   - GUID: `"tenant_id": "123e4567-e89b-12d3-a456-426614174000"`
   - Domain: `"allowed_tenants": "acme,contoso,tenant1"`
   - Mixed: `"allowed_tenants": "acme.com,acme,contoso,tenant1.example.com"`
4. Handle multiple tenants per claim (comma/space/semicolon separated)
5. Validate domain access confirming JWT tenant rights to current domain
6. Return first valid, active tenant matching current domain

**Domain Validation Modes:**
- `None`: Accept any JWT tenant without domain validation
- `ValidateAgainstSubdomain`: Validate against current subdomain
- `ValidateAgainstPath`: Validate against URL path segment
- `ValidateAgainstHeaderOrQuery`: Validate against header/query parameter

**Example JWT claims:**
```json
{
  "sub": "user@example.com",
  "tenant_id": "123e4567-e89b-12d3-a456-426614174000",
  "allowed_tenants": "acme,contoso,tenant1",
  "role": "Admin"
}
```

**Use cases:**
- JWT-based authentication with multi-tenant users
- B2B scenarios with organization switching
- API scenarios with tenant access rights in tokens
- Multi-tenant SaaS with varying user permissions per tenant

**Limitations:**
- Requires JWT authentication configuration
- Domain validation needed to prevent unauthorized access (configurable to `NoOp`)
- JWT must include tenant information in expected format
- Token refresh must maintain tenant claims

## Path Resolver

Extracts tenant from URL path segments: `/tenant1/api/users` becomes `tenant1`

```csharp
services.AddMultiTenantIsolation()
    .WithPathResolutionStrategy(options =>
    {
        options.ExcludedPaths = new[] { "health", "metrics" };
    });
```

**Process:**
1. Check for system admin claims
2. Extract first path segment from request URL
3. Skip excluded paths (health checks, etc.)
4. Resolve tenant using domain lookup

**Use cases:**
- Subdomain setup impractical
- Existing applications with path-based routing
- Shared hosting with complex subdomain configuration

**Limitations:**
- Changes URL structure (`/tenant/feature` vs `/feature`)
- Affects routing and URL generation
- Path segment conflicts with application routes

## System Admin Context

All resolvers check for system admin access first.

System admin detection in every resolver:
```csharp
if (context.IsUserASystemAdmin(options.SystemAdminClaimTypes, options.SystemAdminClaimValue))
{
    return TenantContext.SystemContext();
}
```

**Functionality:**
- Checks JWT claims for system admin roles
- Returns system context bypassing tenant isolation
- Only works with methods marked `[AllowCrossTenantAccess]`

**Use cases:**
- Administrative operations across all tenants
- System maintenance and reporting
- Support scenarios requiring broad access

## Custom Resolvers

Create custom resolvers for specific needs:

```csharp
public class CustomTenantResolver : ITenantResolver
{
    public async Task<TenantContext> ResolveTenantAsync(
        HttpContext context, 
        CancellationToken cancellationToken)
    {
        var tenantId = await SomeCustomLogic(context);
        return TenantContext.ForTenant(tenantId, "Custom");
    }
}

services.AddMultiTenantIsolation()
    .WithCustomResolutionStrategy<CustomTenantResolver>();
```

**When needed:**
- Integration with existing tenant management systems
- Complex resolution logic combining multiple factors
- Legacy application requirements outside standard patterns

## Configuration Tips

**Development vs Production:**
- Development: Header resolver (easy testing)
- Production: Subdomain resolver (better UX)

**Error Handling:**
- All resolvers throw `TenantResolutionException` on failure
- Middleware catches exceptions and returns structured errors
- Check logs for resolution failures during development

## Additional Resources

- [Main Library Documentation](../README.md)
- [Configuration Guide](configuration.md)
- [Features Overview](features.md)