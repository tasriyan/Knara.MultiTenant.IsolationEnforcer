using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Shared;
using TaskMasterPro.Data;

namespace TaskMasterPro.Api.Features.Admin;

[AllowCrossTenantAccess("System admin needs cross-tenant audit access", "SystemAdmin")]
public sealed class GetAuditLogs : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/admin/audit-logs",
			async(ICrossTenantOperationManager crossTenantManager,
					TaskMasterDbContext context,
					CurrentUserService userSvc,
					[FromQuery] Guid ? tenantId = null,
					[FromQuery] DateTime ? fromDate = null,
					[FromQuery] int take = 100) =>
			{
				return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
				{
					var query = context.AdminAuditLogs.AsQueryable();

					if (tenantId.HasValue)
					{
						query = query.Where(log => log.TenantId == tenantId.Value);
					}

					if (fromDate.HasValue)
					{
						query = query.Where(log => log.Timestamp >= fromDate.Value);
					}

					var logs = await query
						.OrderByDescending(log => log.Timestamp)
						.Take(take)
						.ToListAsync();

					return Results.Ok(logs.Select(AdminAuditLogDto.FromEntity).ToList());
				}, $"Admin viewing audit logs for tenant {tenantId}");
			})
		.RequireAuthorization(AuthorizationPolicies.SystemAdmin);
	}
}
