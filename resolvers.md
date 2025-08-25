# Tenant Resolvers Guide

Tenant resolvers determine which tenant a request belongs to. The library includes several built-in resolvers that cover common multi-tenant patterns. Each resolver implements `ITenantResolver` and can be used individually or combined.

## Subdomain Resolver

**Extracts tenant from subdomain**: `tenant1.yourapp.com` → `tenant1`

```csharp
services.AddMultiTenantIsolation()
    .WithSubdomainResolutionStrategy(options =>
    {
        options.ExcludedSubdomains = new[] { "www", "api", "admin" };
        options.SystemAdminClaimValue = "SystemAdmin";
    });
```

**How it works:**
1. Checks if user has system admin claims (returns system context if found)
2. Extracts subdomain from `HttpContext.Request.Host.Host`
3. Skips excluded subdomains like "www" or "api"
4. Looks up tenant using `ITenantLookupService.GetTenantInfoByDomainAsync()`
5. Throws `TenantResolutionException` if subdomain is missing or tenant not found

**Use cases:**
- SaaS applications where each customer gets their own subdomain
- Multi-tenant applications with clear domain separation
- When you control DNS and can set up wildcard subdomains

**Limitations:**
- Requires wildcard DNS setup
- Doesn't work well with localhost development without host file changes
- Limited to what you can express in a subdomain name

## Header Resolver

**Extracts tenant from HTTP headers or query parameters**

```csharp
services.AddMultiTenantIsolation()
    .WithHeaderResolutionStrategy(options =>
    {
        options.IncludedHeaders = new[] { "X-Tenant-ID", "Tenant" };
        options.IncludedQueryParameters = new[] { "tenant", "tenantId" };
    });
```

**How it works:**
1. Checks for system admin claims first
2. Looks for tenant identifier in configured headers
3. Falls back to query parameters if headers not found
4. Accepts both GUID tenant IDs and domain names
5. Resolves using appropriate lookup method

**Use cases:**
- API clients that can set custom headers
- Testing and development scenarios
- Integration with external systems that provide tenant context
- Mobile apps that can control request headers

**Limitations:**
- Requires client cooperation to set headers correctly
- Query parameters are visible in URLs and logs
- Headers can be easily modified by clients

## JWT Resolver

**Extracts tenant from JWT token claims with flexible tenant formats and domain validation**

```csharp
services.AddMultiTenantIsolation()
    .WithJwtResolutionStrategy(options =>
    {
        options.TenantIdClaimTypes = new[] { "tenant_id", "allowed_tenants", "tid" };
        options.SystemAdminClaimValue = "SystemAdmin";
        options.DomainValidationMode = TenantDomainValidationMode.ValidateAgainstSubdomain;
    });
```

**How it works:**
1. Checks for system admin claims in JWT (returns system context if found)
2. Looks for tenant information in configured claim types
3. **Supports multiple tenant formats:**
   - **GUID format**: `"tenant_id": "123e4567-e89b-12d3-a456-426614174000"`
   - **Domain format**: `"allowed_tenants": "acme,contoso,tenant1"`
   - **Mixed format**: `"allowed_tenants": "acme.com,acme,contoso,tenant1.example.com"`
4. **Handles multiple tenants per claim**: Splits comma/space/semicolon separated values
5. **Validates domain access**: Confirms JWT tenant has rights to current request domain
6. Returns the first valid, active tenant that matches current domain

**Domain Validation Modes:**
- `None`: Accept any tenant from JWT without domain validation
- `ValidateAgainstSubdomain`: Validate tenant can access current subdomain
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
- Applications with JWT-based authentication where users belong to multiple tenants
- B2B scenarios where users can switch between different client organizations
- API scenarios where tenant access rights are encoded in authentication tokens
- Multi-tenant SaaS where user permissions vary by tenant

**Limitations:**
- Requires JWT authentication to be configured
- Depends on domain validation to prevent unauthorized tenant access (though domain validation can be set to `TenantDomainValidationMode.NoOp`)
- JWT tokens must include tenant information in expected claim format
- Token refresh scenarios need to maintain tenant claims correctly

## Path Resolver

**Extracts tenant from URL path segments**: `/tenant1/api/users` → `tenant1`

```csharp
services.AddMultiTenantIsolation()
    .WithPathResolutionStrategy(options =>
    {
        options.ExcludedPaths = new[] { "health", "metrics" };
    });
```

**How it works:**
1. Checks for system admin claims
2. Extracts first path segment from request URL
3. Skips excluded paths like health checks
4. Resolves tenant using domain lookup

**Use cases:**
- When subdomains aren't practical
- Existing applications that already use path-based routing
- Shared hosting scenarios where subdomain setup is complex

**Limitations:**
- Changes your URL structure (`/tenant/feature` instead of `/feature`)
- Affects routing and URL generation throughout your app
- Path segment conflicts with application routes

## System Admin Context

**All resolvers check for system admin access first**

System admin detection happens in every resolver:
```csharp
if (context.IsUserASystemAdmin(options.SystemAdminClaimTypes, options.SystemAdminClaimValue))
{
    return TenantContext.SystemContext();
}
```

**What it does:**
- Checks JWT claims for system admin roles
- Returns system context that bypasses tenant isolation
- Only works with methods marked `[AllowCrossTenantAccess]`

**Use cases:**
- Administrative operations that need to work across all tenants
- System maintenance and reporting
- Support scenarios where staff need broad access

## Custom Resolvers

**Create your own resolver for specific needs**

```csharp
public class CustomTenantResolver : ITenantResolver
{
    public async Task<TenantContext> ResolveTenantAsync(HttpContext context, CancellationToken cancellationToken)
    {
        // Your custom logic here
        var tenantId = await SomeCustomLogic(context);
        return TenantContext.ForTenant(tenantId, "Custom");
    }
}

services.AddMultiTenantIsolation()
    .WithCustomResolutionStrategy<CustomTenantResolver>();
```

**When you might need this:**
- Integration with existing tenant management systems
- Complex resolution logic that combines multiple factors
- Legacy application requirements that don't fit standard patterns

## Configuration Tips

**Development vs Production:**
- Use header resolver for development (easy testing)
- Use subdomain resolver for production (better UX)
- Use composite resolver during migration periods

**Error Handling:**
- All resolvers throw `TenantResolutionException` on failure
- Middleware catches these and returns structured error responses
- Check logs for resolution failures during development

**Performance:**
- Enable caching in resolver options when available
- Composite resolvers have overhead - order them by likelihood of success
- System admin checks happen first in all resolvers (fast path)

**Security:**
- Don't rely on headers or query parameters alone in production
- Validate JWT signatures if using JWT resolver
- System admin claims should be properly secured in your identity system

The resolvers handle the common cases, but they're not magic. You still need to set up your DNS, configure your authentication properly, and handle edge cases in your application code.