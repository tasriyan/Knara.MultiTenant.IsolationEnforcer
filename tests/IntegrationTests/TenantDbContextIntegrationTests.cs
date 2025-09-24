using Knara.MultiTenant.IsolationEnforcer.Core;
using Microsoft.EntityFrameworkCore;
using IntegrationTests;

namespace Multitenant.Enforcer.EntityFramework.Tests;

public class TenantDbContextIntegrationTests(TenantDbContextFixture fixture) : IClassFixture<TenantDbContextFixture>
{
	[Fact]
	public async Task GlobalQueryFilter_WithTenantContext_ReturnsOnlyCurrentTenantEntities()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		var tenant1Id = Guid.NewGuid();
		var tenant2Id = Guid.NewGuid();

		scope.SetTenantContext(tenant1Id);
		var tenant1Entities = new[]
		{
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Tenant1 Entity1" },
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Tenant1 Entity2" }
		};
		context.TestEntities.AddRange(tenant1Entities);
		await context.SaveChangesAsync();

		scope.SetTenantContext(tenant2Id);
		var tenant2Entity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenant2Id, Name = "Tenant2 Entity" };
		context.TestEntities.Add(tenant2Entity);
		await context.SaveChangesAsync();

		// Act
		scope.SetTenantContext(tenant1Id);
		var result = await context.TestEntities.ToListAsync();

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldAllBe(e => e.TenantId == tenant1Id);
		result.Select(e => e.Name).ToArray().ShouldBeEquivalentTo(new[] { "Tenant1 Entity1", "Tenant1 Entity2" });
	}

	[Fact]
	public async Task GlobalQueryFilter_WithSystemContext_ReturnsAllEntities()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		var tenant1Id = Guid.NewGuid();
		var tenant2Id = Guid.NewGuid();

		scope.SetTenantContext(tenant1Id);
		var tenant1Entity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Tenant1 Entity" };
		context.TestEntities.Add(tenant1Entity);
		await context.SaveChangesAsync();

		scope.SetTenantContext(tenant2Id);
		var tenant2Entity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenant2Id, Name = "Tenant2 Entity" };
		context.TestEntities.Add(tenant2Entity);
		await context.SaveChangesAsync();

		// Act
		scope.SetSystemContext();
		var result = await context.TestEntities.ToListAsync();

		// Assert
		result.Count.ShouldBeGreaterThanOrEqualTo(2);
		result.Select(e => e.TenantId).Distinct().Count().ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task SaveChanges_WithNewEntity_AutoAssignsTenantId()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		var tenantId = Guid.NewGuid();
		scope.SetTenantContext(tenantId);
		var entity = new TestEntity
		{
			Id = Guid.NewGuid(),
			Name = "New Entity"
			// TenantId not set - should be auto-assigned
		};

		// Act
		context.TestEntities.Add(entity);
		await context.SaveChangesAsync();

		// Assert
		entity.TenantId.ShouldBe(tenantId);

		var saved = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
		saved.ShouldNotBeNull();
		saved.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public async Task SaveChanges_WithWrongTenantId_ThrowsException()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		var currentTenantId = Guid.NewGuid();
		var wrongTenantId = Guid.NewGuid();

		scope.SetTenantContext(currentTenantId);
		var entity = new TestEntity
		{
			Id = Guid.NewGuid(),
			TenantId = wrongTenantId,
			Name = "Wrong Tenant Entity"
		};

		// Act & Assert
		context.TestEntities.Add(entity);
		await Should.ThrowAsync<TenantIsolationViolationException>(
			() => context.SaveChangesAsync());
	}

	[Fact]
	public async Task SaveChanges_WithSystemContext_RequiresExplicitTenantId()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		scope.SetSystemContext();
		var entityWithoutTenantId = new TestEntity
		{
			Id = Guid.NewGuid(),
			Name = "Entity without TenantId"
			// TenantId = Guid.Empty - default value
		};

		// Act & Assert
		context.TestEntities.Add(entityWithoutTenantId);
		await Should.ThrowAsync<TenantIsolationViolationException>(
			() => context.SaveChangesAsync());
	}

	[Fact]
	public async Task SaveChanges_WithSystemContext_AllowsAnyValidTenantId()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		var tenantId = Guid.NewGuid();
		scope.SetSystemContext();
		var entity = new TestEntity
		{
			Id = Guid.NewGuid(),
			TenantId = tenantId,
			Name = "System Created Entity"
		};

		// Act
		context.TestEntities.Add(entity);
		await context.SaveChangesAsync();

		// Assert
		entity.TenantId.ShouldBe(tenantId);

		var saved = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
		saved.ShouldNotBeNull();
		saved.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public async Task Update_WithCrossTenantModification_ThrowsException()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		var tenant1Id = Guid.NewGuid();
		var tenant2Id = Guid.NewGuid();

		scope.SetTenantContext(tenant1Id);
		var entity = new TestEntity
		{
			Id = Guid.NewGuid(),
			TenantId = tenant1Id,
			Name = "Original Entity"
		};
		context.TestEntities.Add(entity);
		await context.SaveChangesAsync();

		// Clear change tracker to simulate loading from different context
		context.ChangeTracker.Clear();

		// Act & Assert
		scope.SetTenantContext(tenant2Id);

		// Simulate loading entity with wrong tenant ID (like from cache or external source)
		var entityFromOtherTenant = new TestEntity
		{
			Id = entity.Id,
			TenantId = tenant1Id, // Still has original tenant ID
			Name = "Modified by wrong tenant"
		};

		context.TestEntities.Update(entityFromOtherTenant);
		await Should.ThrowAsync<TenantIsolationViolationException>(
			() => context.SaveChangesAsync());
	}

	[Fact]
	public async Task Delete_WithCrossTenantDeletion_ThrowsException()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		var tenant1Id = Guid.NewGuid();
		var tenant2Id = Guid.NewGuid();

		scope.SetTenantContext(tenant1Id);
		var entity = new TestEntity
		{
			Id = Guid.NewGuid(),
			TenantId = tenant1Id,
			Name = "Entity to Delete"
		};
		context.TestEntities.Add(entity);
		await context.SaveChangesAsync();

		// Clear change tracker
		context.ChangeTracker.Clear();

		// Act & Assert
		scope.SetTenantContext(tenant2Id);

		// Simulate attempting to delete entity from different tenant
		var entityToDelete = new TestEntity
		{
			Id = entity.Id,
			TenantId = tenant1Id,
			Name = entity.Name
		};

		context.TestEntities.Remove(entityToDelete);
		await Should.ThrowAsync<TenantIsolationViolationException>(
			() => context.SaveChangesAsync());
	}

	[Fact]
	public async Task SystemContext_CanModifyAnyTenantEntity()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		var tenantId = Guid.NewGuid();
		scope.SetTenantContext(tenantId);
		var entity = new TestEntity
		{
			Id = Guid.NewGuid(),
			TenantId = tenantId,
			Name = "Original Name"
		};
		context.TestEntities.Add(entity);
		await context.SaveChangesAsync();

		// Clear change tracker
		context.ChangeTracker.Clear();

		// Act
		scope.SetSystemContext();
		entity.Name = "Modified by System";
		context.TestEntities.Update(entity);
		await context.SaveChangesAsync();

		// Assert
		var updated = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
		updated.ShouldNotBeNull();
		updated.Name.ShouldBe("Modified by System");
	}

	[Fact]
	public async Task SystemContext_CanDeleteAnyTenantEntity()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		var tenantId = Guid.NewGuid();
		scope.SetTenantContext(tenantId);
		var entity = new TestEntity
		{
			Id = Guid.NewGuid(),
			TenantId = tenantId,
			Name = "Entity to Delete"
		};
		context.TestEntities.Add(entity);
		await context.SaveChangesAsync();

		// Clear change tracker
		context.ChangeTracker.Clear();

		// Act
		scope.SetSystemContext();
		context.TestEntities.Remove(entity);
		await context.SaveChangesAsync();

		// Assert
		var deleted = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
		deleted.ShouldBeNull();
	}

	[Fact]
	public async Task ComplexQuery_WithJoinsAndProjections_RespectsTenantFiltering()
	{
		// Arrange
		using var scope = fixture.CreateScope();
		var context = scope.GetTenantDbContext();

		var tenant1Id = Guid.NewGuid();
		var tenant2Id = Guid.NewGuid();

		scope.SetTenantContext(tenant1Id);
		var tenant1Entities = new[]
		{
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Active Entity", IsActive = true },
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Inactive Entity", IsActive = false }
		};
		context.TestEntities.AddRange(tenant1Entities);
		await context.SaveChangesAsync();

		scope.SetTenantContext(tenant2Id);
		var tenant2Entity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenant2Id, Name = "Other Tenant Active", IsActive = true };
		context.TestEntities.Add(tenant2Entity);
		await context.SaveChangesAsync();

		// Act
		scope.SetTenantContext(tenant1Id);
		var result = await context.TestEntities
			.Where(e => e.IsActive)
			.Select(e => new { e.Id, e.Name, e.TenantId })
			.ToListAsync();

		// Assert
		result.Count.ShouldBe(1);
		result[0].Name.ShouldBe("Active Entity");
		result[0].TenantId.ShouldBe(tenant1Id);
	}

	[Fact]
	public async Task MultipleContexts_DoNotLeakDataBetweenTenants()
	{
		// Arrange
		using var scope1 = fixture.CreateScope();
		using var scope2 = fixture.CreateScope();

		var context1 = scope1.GetTenantDbContext();
		var context2 = scope2.GetTenantDbContext();

		var tenant1Id = Guid.NewGuid();
		var tenant2Id = Guid.NewGuid();

		// Setup tenant 1 data
		scope1.SetTenantContext(tenant1Id);
		var tenant1Entity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Tenant1 Entity" };
		context1.TestEntities.Add(tenant1Entity);
		await context1.SaveChangesAsync();

		// Setup tenant 2 data
		scope2.SetTenantContext(tenant2Id);
		var tenant2Entity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenant2Id, Name = "Tenant2 Entity" };
		context2.TestEntities.Add(tenant2Entity);
		await context2.SaveChangesAsync();

		// Act & Assert - Each context should only see its own tenant's data
		var tenant1Data = await context1.TestEntities.ToListAsync();
		tenant1Data.Count.ShouldBe(1);
		tenant1Data[0].TenantId.ShouldBe(tenant1Id);
		tenant1Data[0].Name.ShouldBe("Tenant1 Entity");

		var tenant2Data = await context2.TestEntities.ToListAsync();
		tenant2Data.Count.ShouldBe(1);
		tenant2Data[0].TenantId.ShouldBe(tenant2Id);
		tenant2Data[0].Name.ShouldBe("Tenant2 Entity");
	}
}
