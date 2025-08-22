using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.DataAccess.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
	public void Configure(EntityTypeBuilder<Company> builder)
	{
		builder.ToTable("Companies");

		builder.HasKey(e => e.Id);
		builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
		builder.Property(e => e.Domain).IsRequired().HasMaxLength(100);
		builder.HasIndex(e => e.Domain).IsUnique();
	}
}
