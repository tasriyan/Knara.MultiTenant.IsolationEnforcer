using Knara.MultiTenant.IsolationEnforcer.AspNetCore;
using Knara.MultiTenant.IsolationEnforcer.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.DataAccess;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Admin;

public record TenantStatisticsResponse(
	Guid TenantId,
	string CompanyName,
	CompanyTier Tier,
	int UserCount,
	int ProjectCount,
	int TaskCount,
	DateTime CreatedAt,
	bool IsActive);

[AllowCrossTenantAccess("System admin needs tenant usage statistics", "SystemAdmin")]
public sealed class GetTenantStats : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/admin/tenant-statistics",
			async (ICrossTenantOperationManager crossTenantManager,
					TaskMasterDbContext context,
					ICurrentUserService userSvc,
					[FromQuery] Guid? tenantId = null,
					[FromQuery] DateTime? fromDate = null,
					[FromQuery] int take = 100) =>
			{
				return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
				{
					var statistics = await context.Companies
						.Select(company => new TenantStatisticsResponse(
							company.Id, 
							company.Name, 
							company.Tier,
							context.Users.Count(u => u.TenantId == company.Id),
							context.Projects.Count(p => p.TenantId == company.Id),
							context.ProjectTasks.Count(t => t.TenantId == company.Id),
							company.CreatedAt,
							company.IsActive
						)).ToListAsync();

					return Results.Ok(statistics);
				}, "Admin retrieving tenant statistics");
			})
		.RequireAuthorization(AuthorizationPolicies.SystemAdmin);
	}
}
