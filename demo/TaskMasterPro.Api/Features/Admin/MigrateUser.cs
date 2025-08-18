using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Core;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Entities;
using TaskMasterPro.Api.Shared;

namespace TaskMasterPro.Api.Features.Admin;

public record MigrateUserDto(Guid UserId, Guid FromTenantId, Guid ToTenantId);

[AllowCrossTenantAccess("System admin can migrate users between tenants", "SystemAdmin")]
public sealed class MigrateUser : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost("/api/admin/migrate-user",
			async (ICrossTenantOperationManager crossTenantManager,
					ILogger<MigrateUser> logger,
					TaskMasterDbContext context,
					CurrentUserService userSvc,
					[FromBody] MigrateUserDto dto,
					[FromQuery] Guid? tenantId = null,
					[FromQuery] DateTime? fromDate = null,
					[FromQuery] int take = 100) =>
			{
				return await crossTenantManager.ExecuteCrossTenantOperationAsync(async () =>
				{
					using var transaction = await context.Database.BeginTransactionAsync();

					try
					{
						// Find user in source tenant
						var user = await context.Users
							.FirstOrDefaultAsync(u => u.Id == dto.UserId && u.TenantId == dto.FromTenantId);

						if (user == null)
						{
							return Results.NotFound("User not found in source tenant");
						}

						// Update user's tenant
						user.TenantId = dto.ToTenantId;

						// Log the migration
						var auditLog = new AdminAuditLog
						{
							Id = Guid.NewGuid(),
							TenantId = dto.FromTenantId,
							Action = "USER_MIGRATION",
							EntityType = nameof(User),
							EntityId = user.Id,
							UserEmail = userSvc.UserEmail,
							Details = $"Migrated user {user.Email} from {dto.FromTenantId} to {dto.ToTenantId}",
							Timestamp = DateTime.UtcNow,
							IpAddress = userSvc.IpAddress
						};

						context.AdminAuditLogs.Add(auditLog);
						await context.SaveChangesAsync();
						await transaction.CommitAsync();

						logger.LogWarning("User {UserId} migrated from tenant {FromTenant} to {ToTenant} by {AdminEmail}",
							dto.UserId, dto.FromTenantId, dto.ToTenantId, userSvc.UserEmail);

						return Results.Ok(new { Message = "User migrated successfully" });
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();
						logger.LogError(ex, "Failed to migrate user {UserId}", dto.UserId);
						throw;
					}
				}, $"User migration from {dto.FromTenantId} to {dto.ToTenantId}");
			})
		.RequireAuthorization(AuthorizationPolicies.SystemAdmin);
	}
}
