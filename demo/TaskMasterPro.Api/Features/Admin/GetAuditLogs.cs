using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Admin;

public record AdminAuditLogResponse(Guid Id, Guid TenantId, string Action, string UserEmail, string Details, DateTime Timestamp, string IpAddress);


[AllowCrossTenantAccess("System admin needs cross-tenant audit access", "SystemAdmin")]
public sealed class GetAuditLogs : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/admin/audit-logs",
			async([FromServices] ICrossTenantOperationManager crossTenantManager,
					[FromServices] NotTenantIsolatedAdminDbContext context,
					[FromServices] ICurrentUserService userSvc,
					[FromQuery] Guid ? tenantId,
					[FromQuery] DateTime ? fromDate,
					[FromQuery] int? take) =>
			{
				return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
				{
					var query = context.AuditLogs.AsQueryable();

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
						.Take(take ?? 100)
						.ToListAsync();

					return Results.Ok(logs.Select(l => new AdminAuditLogResponse(Id: l.Id,
																	TenantId: l.TenantId,
																	Action: l.Action,
																	UserEmail: l.UserEmail,
																	Details: l.Details,
																	Timestamp: l.Timestamp,
																	IpAddress: l.IpAddress))
									.ToList());
				}, $"Admin viewing audit logs for tenant {tenantId}");
			})
		.RequireAuthorization(AuthorizationPolicies.SystemAdmin);
	}
}
