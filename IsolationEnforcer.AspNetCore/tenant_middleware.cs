// MultiTenant.Enforcer.AspNetCore/TenantContextMiddleware.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MultiTenant.Enforcer.Core;
using MultiTenant.Enforcer.Core.Exceptions;

namespace MultiTenant.Enforcer.AspNetCore
{
    /// <summary>
    /// Middleware that resolves and sets the tenant context for each request.
    /// Must be registered before authentication middleware.
    /// </summary>
    public class TenantContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantContextMiddleware> _logger;

        public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(
            HttpContext context,
            ITenantContextAccessor tenantAccessor,
            ITenantResolver tenantResolver)
        {
            try
            {
                // Resolve tenant context for this request
                var tenantContext = await tenantResolver.ResolveTenantAsync(context);
                tenantAccessor.SetContext(tenantContext);

                // Add tenant information to response headers for debugging (remove in production)
                if (context.RequestServices.GetService(typeof(IWebHostEnvironment)) is IWebHostEnvironment env &&
                    env.EnvironmentName == "Development")
                {
                    context.Response.Headers.Add("X-Tenant-Context", tenantContext.TenantId.ToString());
                    context.Response.Headers.Add("X-Tenant-Source", tenantContext.ContextSource);
                    context.Response.Headers.Add("X-Is-System-Context", tenantContext.IsSystemContext.ToString());
                }

                // Add tenant info to structured logging context
                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    ["TenantId"] = tenantContext.TenantId,
                    ["IsSystemContext"] = tenantContext.IsSystemContext,
                    ["TenantSource"] = tenantContext.ContextSource,
                    ["RequestPath"] = context.Request.Path.Value ?? "",
                    ["RequestMethod"] = context.Request.Method
                }))
                {
                    _logger.LogDebug("Tenant context set: {TenantId} (System: {IsSystem}) from {Source}",
                        tenantContext.TenantId, tenantContext.IsSystemContext, tenantContext.ContextSource);

                    await _next(context);

                    _logger.LogDebug("Request completed for tenant {TenantId} with status {StatusCode}",
                        tenantContext.TenantId, context.Response.StatusCode);
                }
            }
            catch (TenantResolutionException ex)
            {
                _logger.LogWarning("Failed to resolve tenant for request {Path}: {Error}",
                    context.Request.Path, ex.Message);

                await HandleTenantResolutionError(context, ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in tenant context middleware for request {Path}",
                    context.Request.Path);

                await HandleUnexpectedError(context, ex);
            }
        }

        private static async Task HandleTenantResolutionError(HttpContext context, TenantResolutionException ex)
        {
            context.Response.StatusCode = 400; // Bad Request
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                Error = "Invalid tenant context",
                Message = ex.Message,
                Details = new
                {
                    AttemptedIdentifier = ex.AttemptedTenantIdentifier,
                    ResolutionMethod = ex.ResolutionMethod
                }
            };

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
        }

        private static async Task HandleUnexpectedError(HttpContext context, Exception ex)
        {
            context.Response.StatusCode = 500; // Internal Server Error
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                Error = "Internal server error",
                Message = "An unexpected error occurred while processing the tenant context"
            };

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
        }
    }

    /// <summary>
    /// Interface for resolving tenant context from HTTP requests.
    /// Implement this interface for different tenant identification strategies.
    /// </summary>
    public interface ITenantResolver
    {
        /// <summary>
        /// Resolves the tenant context from the HTTP request.
        /// </summary>
        /// <param name="context">The HTTP context</param>
        /// <returns>The resolved tenant context</returns>
        Task<TenantContext> ResolveTenantAsync(HttpContext context);
    }

    /// <summary>
    /// Tenant resolver that extracts tenant information from JWT token claims.
    /// </summary>
    public class JwtTenantResolver : ITenantResolver
    {
        private readonly ILogger<JwtTenantResolver> _logger;

        public JwtTenantResolver(ILogger<JwtTenantResolver> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TenantContext> ResolveTenantAsync(HttpContext context)
        {
            await Task.CompletedTask; // Make async for consistency

            var user = context.User;

            // Check for system admin access first
            if (user.HasClaim("role", "SystemAdmin") || user.HasClaim("system_access", "true"))
            {
                _logger.LogDebug("System admin access detected in JWT token");
                return TenantContext.SystemContext("JWT-System");
            }

            // Look for tenant ID in standard claims
            var tenantClaim = user.FindFirst("tenant_id") ??
                             user.FindFirst("tenantId") ??
                             user.FindFirst("tid");

            if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tenantId))
            {
                _logger.LogDebug("Tenant {TenantId} resolved from JWT claim {ClaimType}",
                    tenantId, tenantClaim.Type);
                return TenantContext.ForTenant(tenantId, "JWT");
            }

            throw new TenantResolutionException(
                "No tenant information found in JWT token",
                "JWT token missing tenant_id claim",
                "JWT");
        }
    }

    /// <summary>
    /// Tenant resolver that extracts tenant information from subdomain.
    /// </summary>
    public class SubdomainTenantResolver : ITenantResolver
    {
        private readonly ILogger<SubdomainTenantResolver> _logger;
        private readonly ITenantLookupService _tenantLookupService;

        public SubdomainTenantResolver(
            ILogger<SubdomainTenantResolver> logger,
            ITenantLookupService tenantLookupService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tenantLookupService = tenantLookupService ?? throw new ArgumentNullException(nameof(tenantLookupService));
        }

        public async Task<TenantContext> ResolveTenantAsync(HttpContext context)
        {
            // Check for system admin in JWT first
            if (context.User.HasClaim("role", "SystemAdmin"))
            {
                return TenantContext.SystemContext("SystemAdmin-JWT");
            }

            var host = context.Request.Host.Host;
            var subdomain = ExtractSubdomain(host);

            if (string.IsNullOrEmpty(subdomain))
            {
                throw new TenantResolutionException(
                    "No subdomain found in request",
                    host,
                    "Subdomain");
            }

            var tenantId = await _tenantLookupService.GetTenantIdByDomainAsync(subdomain);
            if (tenantId == null)
            {
                throw new TenantResolutionException(
                    $"No active tenant found for domain: {subdomain}",
                    subdomain,
                    "Subdomain");
            }

            _logger.LogDebug("Tenant {TenantId} resolved from subdomain {Subdomain}",
                tenantId, subdomain);

            return TenantContext.ForTenant(tenantId.Value, $"Subdomain:{subdomain}");
        }

        private static string ExtractSubdomain(string host)
        {
            // For local development: acme.localhost:5000 -> acme
            // For production: acme.example.com -> acme

            var parts = host.Split('.');

            if (parts.Length >= 2 && parts[0] != "www" && parts[0] != "localhost")
            {
                return parts[0];
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Composite tenant resolver that tries multiple resolution strategies.
    /// </summary>
    public class CompositeTenantResolver : ITenantResolver
    {
        private readonly ITenantResolver[] _resolvers;
        private readonly ILogger<CompositeTenantResolver> _logger;

        public CompositeTenantResolver(ITenantResolver[] resolvers, ILogger<CompositeTenantResolver> logger)
        {
            _resolvers = resolvers ?? throw new ArgumentNullException(nameof(resolvers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TenantContext> ResolveTenantAsync(HttpContext context)
        {
            var exceptions = new List<Exception>();

            foreach (var resolver in _resolvers)
            {
                try
                {
                    var result = await resolver.ResolveTenantAsync(context);
                    _logger.LogDebug("Tenant resolved using {ResolverType}: {TenantId}",
                        resolver.GetType().Name, result.TenantId);
                    return result;
                }
                catch (TenantResolutionException ex)
                {
                    exceptions.Add(ex);
                    _logger.LogDebug("Tenant resolution failed with {ResolverType}: {Error}",
                        resolver.GetType().Name, ex.Message);
                }
            }

            var errorMessage = $"All tenant resolution strategies failed. Tried: {string.Join(", ", _resolvers.Select(r => r.GetType().Name))}";
            _logger.LogWarning("Failed to resolve tenant using any strategy");

            throw new TenantResolutionException(errorMessage, null, "Composite");
        }
    }

    /// <summary>
    /// Service for looking up tenant information from storage.
    /// </summary>
    public interface ITenantLookupService
    {
        /// <summary>
        /// Gets the tenant ID for the specified domain.
        /// </summary>
        /// <param name="domain">The domain to lookup</param>
        /// <returns>The tenant ID if found, null otherwise</returns>
        Task<Guid?> GetTenantIdByDomainAsync(string domain);

        /// <summary>
        /// Gets tenant information by ID.
        /// </summary>
        /// <param name="tenantId">The tenant ID</param>
        /// <returns>Tenant information if found, null otherwise</returns>
        Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId);
    }

    /// <summary>
    /// Basic tenant information.
    /// </summary>
    public class TenantInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
