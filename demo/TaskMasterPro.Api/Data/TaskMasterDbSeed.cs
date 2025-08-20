using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.Data;

public static class TaskMasterDbSeed
{
	public static void SeedData(ModelBuilder modelBuilder)
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

		// User IDs
		var johnDoeId = Guid.Parse("11111111-1111-1111-1111-111111111113");
		var janeSmithId = Guid.Parse("11111111-1111-1111-1111-111111111114");
		var bobWilsonId = Guid.Parse("22222222-2222-2222-2222-222222222221");

		// Seed test users
		modelBuilder.Entity<User>().HasData(
			// Acme users
			new User
			{
				Id = johnDoeId,
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
				Id = janeSmithId,
				TenantId = company1Id,
				Email = "jane.smith@acme.com",
				FirstName = "Jane",
				LastName = "Smith",
				Role = UserRole.ProjectManager,
				CreatedAt = DateTime.UtcNow.AddDays(-80),
				IsActive = true
			},
			// Add more Acme users
			new User
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111115"),
				TenantId = company1Id,
				Email = "mike.johnson@acme.com",
				FirstName = "Mike",
				LastName = "Johnson",
				Role = UserRole.Member,
				CreatedAt = DateTime.UtcNow.AddDays(-75),
				IsActive = true
			},
			new User
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111116"),
				TenantId = company1Id,
				Email = "sarah.davis@acme.com",
				FirstName = "Sarah",
				LastName = "Davis",
				Role = UserRole.Member,
				CreatedAt = DateTime.UtcNow.AddDays(-70),
				IsActive = true
			},
			// Tech Innovations users
			new User
			{
				Id = bobWilsonId,
				TenantId = company2Id,
				Email = "bob.wilson@techinnovations.com",
				FirstName = "Bob",
				LastName = "Wilson",
				Role = UserRole.Admin,
				CreatedAt = DateTime.UtcNow.AddDays(-55),
				IsActive = true
			},
			new User
			{
				Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
				TenantId = company2Id,
				Email = "lisa.chen@techinnovations.com",
				FirstName = "Lisa",
				LastName = "Chen",
				Role = UserRole.ProjectManager,
				CreatedAt = DateTime.UtcNow.AddDays(-50),
				IsActive = true
			},
			new User
			{
				Id = Guid.Parse("22222222-2222-2222-2222-222222222223"),
				TenantId = company2Id,
				Email = "david.brown@techinnovations.com",
				FirstName = "David",
				LastName = "Brown",
				Role = UserRole.Member,
				CreatedAt = DateTime.UtcNow.AddDays(-45),
				IsActive = true
			}
		);

		// Project IDs
		var acmeProject1Id = Guid.Parse("11111111-1111-1111-1111-111111111121");
		var acmeProject2Id = Guid.Parse("11111111-1111-1111-1111-111111111122");
		var techProject1Id = Guid.Parse("22222222-2222-2222-2222-222222222231");
		var techProject2Id = Guid.Parse("22222222-2222-2222-2222-222222222232");

		// Seed projects
		modelBuilder.Entity<Project>().HasData(
			// Acme Corporation projects
			new Project
			{
				Id = acmeProject1Id,
				TenantId = company1Id,
				Name = "Customer Portal Redesign",
				Description = "Complete redesign of the customer portal with modern UI/UX",
				ProjectManagerId = janeSmithId,
				StartDate = DateTime.UtcNow.AddDays(-30),
				EndDate = DateTime.UtcNow.AddDays(60),
				Status = ProjectStatus.Active,
				CreatedAt = DateTime.UtcNow.AddDays(-30)
			},
			new Project
			{
				Id = acmeProject2Id,
				TenantId = company1Id,
				Name = "Mobile App Development",
				Description = "Native mobile application for iOS and Android platforms",
				ProjectManagerId = janeSmithId,
				StartDate = DateTime.UtcNow.AddDays(-15),
				EndDate = DateTime.UtcNow.AddDays(90),
				Status = ProjectStatus.Planning,
				CreatedAt = DateTime.UtcNow.AddDays(-15)
			},
			// Tech Innovations projects
			new Project
			{
				Id = techProject1Id,
				TenantId = company2Id,
				Name = "AI Analytics Platform",
				Description = "Machine learning platform for business analytics and insights",
				ProjectManagerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
				StartDate = DateTime.UtcNow.AddDays(-45),
				EndDate = DateTime.UtcNow.AddDays(120),
				Status = ProjectStatus.Active,
				CreatedAt = DateTime.UtcNow.AddDays(-45)
			},
			new Project
			{
				Id = techProject2Id,
				TenantId = company2Id,
				Name = "Cloud Migration",
				Description = "Migration of legacy systems to cloud infrastructure",
				ProjectManagerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
				StartDate = DateTime.UtcNow.AddDays(-20),
				EndDate = DateTime.UtcNow.AddDays(75),
				Status = ProjectStatus.Active,
				CreatedAt = DateTime.UtcNow.AddDays(-20)
			}
		);

		// Seed project tasks
		modelBuilder.Entity<ProjectTask>().HasData(
			// Tasks for Customer Portal Redesign (Acme)
			new ProjectTask
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111131"),
				TenantId = company1Id,
				ProjectId = acmeProject1Id,
				Title = "User Research and Analysis",
				Description = "Conduct user interviews and analyze current portal usage patterns",
				AssignedToId = Guid.Parse("11111111-1111-1111-1111-111111111115"),
				Priority = ProjectTaskPriority.High,
				Status = ProjectTaskStatus.Done,
				DueDate = DateTime.UtcNow.AddDays(-10),
				CreatedAt = DateTime.UtcNow.AddDays(-30),
				CompletedAt = DateTime.UtcNow.AddDays(-12)
			},
			new ProjectTask
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111132"),
				TenantId = company1Id,
				ProjectId = acmeProject1Id,
				Title = "UI/UX Design Mockups",
				Description = "Create wireframes and high-fidelity mockups for new portal design",
				AssignedToId = Guid.Parse("11111111-1111-1111-1111-111111111116"),
				Priority = ProjectTaskPriority.High,
				Status = ProjectTaskStatus.InProgress,
				DueDate = DateTime.UtcNow.AddDays(5),
				CreatedAt = DateTime.UtcNow.AddDays(-25)
			},
			new ProjectTask
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111133"),
				TenantId = company1Id,
				ProjectId = acmeProject1Id,
				Title = "Frontend Development Setup",
				Description = "Set up development environment and project structure",
				AssignedToId = Guid.Parse("11111111-1111-1111-1111-111111111115"),
				Priority = ProjectTaskPriority.Medium,
				Status = ProjectTaskStatus.ToDo,
				DueDate = DateTime.UtcNow.AddDays(15),
				CreatedAt = DateTime.UtcNow.AddDays(-20)
			},
			new ProjectTask
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111134"),
				TenantId = company1Id,
				ProjectId = acmeProject1Id,
				Title = "API Integration Planning",
				Description = "Design API endpoints and integration strategy",
				AssignedToId = johnDoeId,
				Priority = ProjectTaskPriority.Medium,
				Status = ProjectTaskStatus.ToDo,
				DueDate = DateTime.UtcNow.AddDays(20),
				CreatedAt = DateTime.UtcNow.AddDays(-18)
			},

			// Tasks for Mobile App Development (Acme)
			new ProjectTask
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111141"),
				TenantId = company1Id,
				ProjectId = acmeProject2Id,
				Title = "Market Research",
				Description = "Research competitor apps and identify key features",
				AssignedToId = janeSmithId,
				Priority = ProjectTaskPriority.High,
				Status = ProjectTaskStatus.InReview,
				DueDate = DateTime.UtcNow.AddDays(-2),
				CreatedAt = DateTime.UtcNow.AddDays(-15)
			},
			new ProjectTask
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111142"),
				TenantId = company1Id,
				ProjectId = acmeProject2Id,
				Title = "Technical Architecture Design",
				Description = "Define mobile app architecture and technology stack",
				AssignedToId = johnDoeId,
				Priority = ProjectTaskPriority.Critical,
				Status = ProjectTaskStatus.ToDo,
				DueDate = DateTime.UtcNow.AddDays(10),
				CreatedAt = DateTime.UtcNow.AddDays(-10)
			},

			// Tasks for AI Analytics Platform (Tech Innovations)
			new ProjectTask
			{
				Id = Guid.Parse("22222222-2222-2222-2222-222222222241"),
				TenantId = company2Id,
				ProjectId = techProject1Id,
				Title = "Data Pipeline Development",
				Description = "Build data ingestion and processing pipelines",
				AssignedToId = Guid.Parse("22222222-2222-2222-2222-222222222223"),
				Priority = ProjectTaskPriority.Critical,
				Status = ProjectTaskStatus.InProgress,
				DueDate = DateTime.UtcNow.AddDays(14),
				CreatedAt = DateTime.UtcNow.AddDays(-40)
			},
			new ProjectTask
			{
				Id = Guid.Parse("22222222-2222-2222-2222-222222222242"),
				TenantId = company2Id,
				ProjectId = techProject1Id,
				Title = "Machine Learning Model Training",
				Description = "Train and validate ML models for business analytics",
				AssignedToId = bobWilsonId,
				Priority = ProjectTaskPriority.High,
				Status = ProjectTaskStatus.ToDo,
				DueDate = DateTime.UtcNow.AddDays(30),
				CreatedAt = DateTime.UtcNow.AddDays(-35)
			},
			new ProjectTask
			{
				Id = Guid.Parse("22222222-2222-2222-2222-222222222243"),
				TenantId = company2Id,
				ProjectId = techProject1Id,
				Title = "Dashboard Frontend Development",
				Description = "Create interactive dashboards for analytics visualization",
				AssignedToId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
				Priority = ProjectTaskPriority.Medium,
				Status = ProjectTaskStatus.ToDo,
				DueDate = DateTime.UtcNow.AddDays(45),
				CreatedAt = DateTime.UtcNow.AddDays(-30)
			},

			// Tasks for Cloud Migration (Tech Innovations)
			new ProjectTask
			{
				Id = Guid.Parse("22222222-2222-2222-2222-222222222251"),
				TenantId = company2Id,
				ProjectId = techProject2Id,
				Title = "Infrastructure Assessment",
				Description = "Analyze current infrastructure and migration requirements",
				AssignedToId = Guid.Parse("22222222-2222-2222-2222-222222222223"),
				Priority = ProjectTaskPriority.Critical,
				Status = ProjectTaskStatus.Done,
				DueDate = DateTime.UtcNow.AddDays(-15),
				CreatedAt = DateTime.UtcNow.AddDays(-20),
				CompletedAt = DateTime.UtcNow.AddDays(-16)
			},
			new ProjectTask
			{
				Id = Guid.Parse("22222222-2222-2222-2222-222222222252"),
				TenantId = company2Id,
				ProjectId = techProject2Id,
				Title = "Cloud Environment Setup",
				Description = "Configure cloud infrastructure and security settings",
				AssignedToId = bobWilsonId,
				Priority = ProjectTaskPriority.High,
				Status = ProjectTaskStatus.InProgress,
				DueDate = DateTime.UtcNow.AddDays(7),
				CreatedAt = DateTime.UtcNow.AddDays(-18)
			},
			new ProjectTask
			{
				Id = Guid.Parse("22222222-2222-2222-2222-222222222253"),
				TenantId = company2Id,
				ProjectId = techProject2Id,
				Title = "Data Migration Testing",
				Description = "Test data migration procedures and validate data integrity",
				AssignedToId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
				Priority = ProjectTaskPriority.High,
				Status = ProjectTaskStatus.ToDo,
				DueDate = DateTime.UtcNow.AddDays(21),
				CreatedAt = DateTime.UtcNow.AddDays(-15)
			}
		);
	}
}
