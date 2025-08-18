using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Data.Configurations;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
	public void Configure(EntityTypeBuilder<ProjectTask> builder)
	{
		builder.ToTable("Tasks");

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
		builder.Property(e => e.Description).HasMaxLength(2000);

		builder.HasOne(e => e.Project)
			  .WithMany(p => p.Tasks)
			  .HasForeignKey(e => e.ProjectId)
			  .OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(e => e.AssignedTo)
			  .WithMany()
			  .HasForeignKey(e => e.AssignedToId)
			  .OnDelete(DeleteBehavior.SetNull);

		builder.HasIndex(e => new { e.TenantId, e.ProjectId });
		builder.HasIndex(e => new { e.TenantId, e.AssignedToId });
		builder.HasIndex(e => new { e.TenantId, e.Status });
		builder.HasIndex(e => new { e.TenantId, e.DueDate });
	}
}
