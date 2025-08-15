using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;
using TaskMasterPro.Data;

namespace TaskMasterPro.Api.Features.Admin;

public record CompanyResponse(Guid Id, string Name, CompanyTier Tier);

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

					return Results.Ok(companies.Select(c => new CompanyResponse(c.Id, c.Name, c.Tier)).ToList());
				}, "Admin viewing all companies");
			})
		.RequireAuthorization(AuthorizationPolicies.SystemAdmin);
	}
}
