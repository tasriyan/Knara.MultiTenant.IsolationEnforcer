using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
	public void Configure(EntityTypeBuilder<User> builder)
	{
		builder.ToTable("Users");

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Email).IsRequired().HasMaxLength(200);
		builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
		builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);

		builder.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
		builder.HasIndex(e => new { e.TenantId, e.Role });
	}
}
