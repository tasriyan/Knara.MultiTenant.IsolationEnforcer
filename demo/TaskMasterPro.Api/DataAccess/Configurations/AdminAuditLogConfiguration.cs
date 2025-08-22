using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.DataAccess.Configurations;

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
	public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
	{
		builder.ToTable("AuditLogs");

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Action).IsRequired().HasMaxLength(100);
		builder.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
		builder.Property(e => e.UserEmail).IsRequired().HasMaxLength(200);
		builder.Property(e => e.Details).HasMaxLength(2000);
		builder.Property(e => e.IpAddress).HasMaxLength(45);

		builder.HasIndex(e => new { e.TenantId, e.Timestamp });
		builder.HasIndex(e => new { e.EntityType, e.EntityId });
		builder.HasIndex(e => e.UserId);
	}
}
