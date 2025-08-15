using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Shared;
using TaskMasterPro.Data;

namespace TaskMasterPro.Api.Features.Admin;

[AllowCrossTenantAccess("System admin needs tenant usage statistics", "SystemAdmin")]
public sealed class GetTenantStats : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/admin/tenant-statistics",
			async (ICrossTenantOperationManager crossTenantManager,
					TaskMasterDbContext context,
					CurrentUserService userSvc,
					[FromQuery] Guid? tenantId = null,
					[FromQuery] DateTime? fromDate = null,
					[FromQuery] int take = 100) =>
			{
				return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
				{
					var statistics = await context.Companies
						.Select(company => new TenantStatisticsDto
						{
							TenantId = company.Id,
							CompanyName = company.Name,
							Tier = company.Tier,
							UserCount = context.Users.Count(u => u.TenantId == company.Id),
							ProjectCount = context.Projects.Count(p => p.TenantId == company.Id),
							TaskCount = context.ProjectTasks.Count(t => t.TenantId == company.Id),
							CreatedAt = company.CreatedAt,
							IsActive = company.IsActive
						})
						.ToListAsync();

					return Results.Ok(statistics);
				}, "Admin retrieving tenant statistics");
			})
		.RequireAuthorization(AuthorizationPolicies.SystemAdmin);
	}
}
