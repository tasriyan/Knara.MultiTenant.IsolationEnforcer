using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.Core;
using Multitenant.Enforcer.EntityFramework;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Data;

public class TaskMasterDbContext : TenantDbContext
{
	public DbSet<Company> Companies { get; set; }
	public DbSet<User> Users { get; set; }
	public DbSet<Project> Projects { get; set; }
	public DbSet<ProjectTask> ProjectTasks { get; set; }
	public DbSet<TimeEntry> TimeEntries { get; set; }
	public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

	public TaskMasterDbContext(DbContextOptions<TaskMasterDbContext> options,
		ITenantContextAccessor tenantAccessor,
		ILogger<TaskMasterDbContext> logger)
		: base(options, tenantAccessor, logger)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder); // Apply tenant isolation

		ConfigureEntities(modelBuilder);
		SeedData(modelBuilder);
	}

	private void ConfigureEntities(ModelBuilder modelBuilder)
	{
		// Company configuration
		modelBuilder.Entity<Company>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.Property(e => e.Domain).IsRequired().HasMaxLength(100);
			entity.HasIndex(e => e.Domain).IsUnique();
		});

		// User configuration
		modelBuilder.Entity<User>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
			entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
			entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);

			// Composite index for performance
			entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
			entity.HasIndex(e => new { e.TenantId, e.Role });
		});

		// Project configuration
		modelBuilder.Entity<Project>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.Property(e => e.Description).HasMaxLength(1000);

			entity.HasOne(e => e.ProjectManager)
				  .WithMany()
				  .HasForeignKey(e => e.ProjectManagerId)
				  .OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(e => new { e.TenantId, e.Status });
			entity.HasIndex(e => new { e.TenantId, e.ProjectManagerId });
		});

		// Task configuration
		modelBuilder.Entity<ProjectTask>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
			entity.Property(e => e.Description).HasMaxLength(2000);

			entity.HasOne(e => e.Project)
				  .WithMany(p => p.Tasks)
				  .HasForeignKey(e => e.ProjectId)
				  .OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(e => e.AssignedTo)
				  .WithMany()
				  .HasForeignKey(e => e.AssignedToId)
				  .OnDelete(DeleteBehavior.SetNull);

			entity.HasIndex(e => new { e.TenantId, e.ProjectId });
			entity.HasIndex(e => new { e.TenantId, e.AssignedToId });
			entity.HasIndex(e => new { e.TenantId, e.Status });
			entity.HasIndex(e => new { e.TenantId, e.DueDate });
		});

		// TimeEntry configuration
		modelBuilder.Entity<TimeEntry>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Description).HasMaxLength(500);

			entity.HasOne(e => e.Task)
				  .WithMany(t => t.TimeEntries)
				  .HasForeignKey(e => e.TaskId)
				  .OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(e => e.User)
				  .WithMany()
				  .HasForeignKey(e => e.UserId)
				  .OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(e => new { e.TenantId, e.UserId });
			entity.HasIndex(e => new { e.TenantId, e.TaskId });
			entity.HasIndex(e => new { e.TenantId, e.StartTime });
		});

		// AdminAuditLog configuration
		modelBuilder.Entity<AdminAuditLog>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
			entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
			entity.Property(e => e.UserEmail).IsRequired().HasMaxLength(200);
			entity.Property(e => e.Details).HasMaxLength(2000);
			entity.Property(e => e.IpAddress).HasMaxLength(45);

			entity.HasIndex(e => new { e.TenantId, e.Timestamp });
			entity.HasIndex(e => new { e.EntityType, e.EntityId });
			entity.HasIndex(e => e.UserId);
		});
	}

	private void SeedData(ModelBuilder modelBuilder)
	{
		// Seed test companies
		var company1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var company2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

		modelBuilder.Entity<Company>().HasData(
			new Company
			{
				Id = company1Id,
				Name = "Acme Corporation",
				Domain = "acme",
				CreatedAt = DateTime.UtcNow.AddDays(-90),
				Tier = CompanyTier.Professional,
				IsActive = true
			},
			new Company
			{
				Id = company2Id,
				Name = "Tech Innovations Inc",
				Domain = "techinnovations",
				CreatedAt = DateTime.UtcNow.AddDays(-60),
				Tier = CompanyTier.Enterprise,
				IsActive = true
			}
		);

		// Seed test users
		modelBuilder.Entity<User>().HasData(
			// Acme users
			new User
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111112"),
				TenantId = company1Id,
				Email = "john.doe@acme.com",
				FirstName = "John",
				LastName = "Doe",
				Role = UserRole.Admin,
				CreatedAt = DateTime.UtcNow.AddDays(-85),
				IsActive = true
			},
			new User
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111113"),
				TenantId = company1Id,
				Email = "jane.smith@acme.com",
				FirstName = "Jane",
				LastName = "Smith",
				Role = UserRole.ProjectManager,
				CreatedAt = DateTime.UtcNow.AddDays(-80),
				IsActive = true
			},
			// Tech Innovations users
			new User
			{
				Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
				TenantId = company2Id,
				Email = "bob.wilson@techinnovations.com",
				FirstName = "Bob",
				LastName = "Wilson",
				Role = UserRole.Admin,
				CreatedAt = DateTime.UtcNow.AddDays(-55),
				IsActive = true
			}
		);
	}
}
