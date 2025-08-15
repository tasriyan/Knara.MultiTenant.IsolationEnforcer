using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Shared;
using TaskMasterPro.Data;

namespace TaskMasterPro.Api.Features.Admin;

[AllowCrossTenantAccess("System admin needs to view all companies", "SystemAdmin")]
public sealed class GetAllCompanies : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/admin/companies",
			async (ICrossTenantOperationManager crossTenantManager,
					TaskMasterDbContext context,
					CurrentUserService userSvc) =>
			{
				return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
				{
					// In system context, we can access the non-tenant-isolated Companies table
					var companies = await context.Companies
													.AsNoTracking()
													.OrderBy(c => c.Name)
													.ToListAsync();

					return Results.Ok(companies.Select(CompanyDto.FromEntity).ToList());
				}, "Admin viewing all companies");
			})
		.RequireAuthorization(AuthorizationPolicies.SystemAdmin);
	}
}
