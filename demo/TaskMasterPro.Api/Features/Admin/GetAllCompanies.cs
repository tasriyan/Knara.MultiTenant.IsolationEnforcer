using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Data.Configurations;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Admin;

//
// DEMO:
//	

public record CompanyResponse(Guid Id, string Name, CompanyTier Tier);

// DEMO:
// Using dbcontext directly - not using TenantRepository
// SHOULD COMPILE because SafeDbContext is derived from TenantDbContext THAT ENSURES TENANT ISOLATION

[AllowCrossTenantAccess("System admin needs to view all companies", "SystemAdmin")]
public sealed class GetAllCompanies : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/admin/companies",
			async (ICrossTenantOperationManager crossTenantManager,
					SafeDbContext context,
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

// DEMO:
// Uses UNSAFE db context
// HOWEVER: SHOULD COMPILE because UnsafeDbContext does not contain entities that are tenant-isolated (e.g. ITenantIsolated)
[AllowCrossTenantAccess("System admin needs to view all companies", "SystemAdmin")]
public sealed class GetAllCompaniesUnsafe : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapGet("/api/admin/companies",
			async (ICrossTenantOperationManager crossTenantManager,
					UnsafeDbContext context,
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

public class UnsafeDbContext : DbContext
{
	public UnsafeDbContext(DbContextOptions<DbContext> options) : base(options) { }

	public DbSet<Company> Companies { get; set; }
	public DbSet<AdminAuditLog> AuditLogs { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new AdminAuditLogConfiguration());
		modelBuilder.ApplyConfiguration(new CompanyConfiguration());

		TaskMasterDbSeed.SeedData(modelBuilder);
	}
}

public class SafeDbContext : TenantDbContext
{
	public SafeDbContext(DbContextOptions<SafeDbContext> options,
							ITenantContextAccessor tenantAccessor,
							ILogger<SafeDbContext> logger)
		: base(options, tenantAccessor, logger)
	{
	}

	public DbSet<Company> Companies { get; set; }
	public DbSet<AdminAuditLog> AuditLogs { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder); // Apply tenant isolation

		modelBuilder.ApplyConfiguration(new AdminAuditLogConfiguration());
		modelBuilder.ApplyConfiguration(new CompanyConfiguration());

		TaskMasterDbSeed.SeedData(modelBuilder);
	}
}

