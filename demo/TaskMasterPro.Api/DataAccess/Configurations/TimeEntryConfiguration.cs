using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.DataAccess.Configurations;

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
	public void Configure(EntityTypeBuilder<TimeEntry> builder)
	{
		builder.ToTable("TimeEntries");

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Description).HasMaxLength(500);

		builder.HasOne(e => e.Task)
			  .WithMany(t => t.TimeEntries)
			  .HasForeignKey(e => e.TaskId)
			  .OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(e => e.User)
			  .WithMany()
			  .HasForeignKey(e => e.UserId)
			  .OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(e => new { e.TenantId, e.UserId });
		builder.HasIndex(e => new { e.TenantId, e.TaskId });
		builder.HasIndex(e => new { e.TenantId, e.StartTime });
	}
}
