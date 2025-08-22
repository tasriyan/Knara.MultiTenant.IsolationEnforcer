using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.DataAccess.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
	public void Configure(EntityTypeBuilder<Project> builder)
	{
		builder.ToTable("Projects");

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
		builder.Property(e => e.Description).HasMaxLength(1000);

		builder.HasOne(e => e.ProjectManager)
			  .WithMany()
			  .HasForeignKey(e => e.ProjectManagerId)
			  .OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(e => new { e.TenantId, e.Status });
		builder.HasIndex(e => new { e.TenantId, e.ProjectManagerId });
	}
}
