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
