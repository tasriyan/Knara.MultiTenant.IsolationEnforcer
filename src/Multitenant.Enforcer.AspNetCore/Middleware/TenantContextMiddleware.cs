using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.TenantResolvers;
using System.Text.Json;

namespace Multitenant.Enforcer.AspnetCore;

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
			var tenantContext = await tenantResolver.GetTenantContextAsync(context, CancellationToken.None);
			tenantAccessor.SetContext(tenantContext);

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
		context.Response.StatusCode = 400; 
		context.Response.ContentType = "application/json";

		var errorResponse = new
		{
			Error = "Invalid tenant context",
			ex.Message,
			Details = new
			{
				AttemptedIdentifier = ex.AttemptedTenantIdentifier,
				ex.ResolutionMethod
			}
		};

		await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
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

		await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
	}
}
