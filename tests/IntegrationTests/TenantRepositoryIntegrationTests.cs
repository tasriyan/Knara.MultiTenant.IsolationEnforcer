using Knara.MultiTenant.IsolationEnforcer.Core;
using Microsoft.EntityFrameworkCore;
using IntegrationTests;

namespace Multitenant.Enforcer.EntityFramework.Tests;

public class TenantRepositoryIntegrationTests(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
{
	[Fact]
    public async Task CountAsync_WithPredicate_ReturnsCorrectCount()
    {
        // Arrange
		using var scope = fixture.CreateScope();      
        var repository = scope.GetRepository();
        var context = scope.GetDbContext();

		var tenantId = Guid.NewGuid();
		scope.SetTenantContext(tenantId);
		var entities = new[]
        {
            new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Entity 1", IsActive = true },
            new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Entity 2", IsActive = true },
            new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Entity 3", IsActive = false }
        };
        context.TestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act
        var totalCount = await repository.CountAsync();
        var activeCount = await repository.CountAsync(e => e.IsActive);

        // Assert
        totalCount.ShouldBe(3);
        activeCount.ShouldBe(2);
    }

    [Fact]
    public async Task AnyAsync_WithPredicate_ReturnsCorrectResult()
    {
        // Arrange
		using var scope = fixture.CreateScope();     
        var repository = scope.GetRepository();
        var context = scope.GetDbContext();

		var tenantId = Guid.NewGuid();
		scope.SetTenantContext(tenantId);
		var entity = new TestEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Special Entity",
            IsActive = true
        };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var hasActive = await repository.AnyAsync(e => e.IsActive);
        var hasInactive = await repository.AnyAsync(e => !e.IsActive);

        // Assert
        hasActive.ShouldBeTrue();
        hasInactive.ShouldBeFalse();
    }

    [Fact]
    public async Task Query_ReturnsOnlyCurrentTenantEntities()
    {
        // Arrange
		using var scope = fixture.CreateScope();
        var repository = scope.GetRepository();
        var context = scope.GetDbContext();

		var tenant1Id = Guid.NewGuid();
		var tenant2Id = Guid.NewGuid();

		scope.SetTenantContext(tenant1Id);
		var tenant1Entities = new[]
        {
            new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Tenant1 Entity1" },
            new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Tenant1 Entity2" }
        };
        var tenant2Entity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenant2Id, Name = "Tenant2 Entity" };
        context.TestEntities.AddRange(tenant1Entities);
		await context.SaveChangesAsync();

		scope.SetTenantContext(tenant2Id);
		context.TestEntities.Add(tenant2Entity);
		await context.SaveChangesAsync();

		// Act
		scope.SetTenantContext(tenant1Id);
		var result = await repository.Query().ToListAsync();

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(e => e.TenantId == tenant1Id);
    }

    public class AddAsync(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
	{
		[Fact]
		public async Task If_ValidEntityHasNoTenentId_ThenAutoAssignsTenantId()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenantId = Guid.NewGuid();
			scope.SetTenantContext(tenantId);
			var entity = new TestEntity
			{
				Id = Guid.NewGuid(),
				Name = "New Entity"
			};

			// Act
			var result = await repository.AddAsync(entity);

			// Assert
			result.TenantId.ShouldBe(tenantId);

			var saved = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
			saved.ShouldNotBeNull();
			saved.TenantId.ShouldBe(tenantId);
		}

		[Fact]
		public async Task If_TenantIdDoesNotMatchOneOnEntity_ThrowsException()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();

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
			await Should.ThrowAsync<TenantIsolationViolationException>(
				() => repository.AddAsync(entity));
		}

		[Fact]
		public async Task AddValidEntitesRange_AddsAllWithCorrectTenantId()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenantId = Guid.NewGuid();
			scope.SetTenantContext(tenantId);
			var entities = new[]
			{
			new TestEntity { Id = Guid.NewGuid(), Name = "Entity 1" },
			new TestEntity { Id = Guid.NewGuid(), Name = "Entity 2" }
		};

			// Act
			var result = await repository.AddRangeAsync(entities);

			// Assert
			result.Count.ShouldBe(2);
			result.ShouldAllBe(e => e.TenantId == tenantId);

			var saved = await context.TestEntities.Where(e => entities.Select(x => x.Id).Contains(e.Id)).ToListAsync();
			saved.Count.ShouldBe(2);
			saved.ShouldAllBe(e => e.TenantId == tenantId);
		}
	}

    public class UpdateAsync_And_UpdateRangeAsync(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
	{
		[Fact]
		public async Task ShouldUpdatesSuccessfully()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

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
			context.Entry(entity).State = EntityState.Detached;

			entity.Name = "Updated Name";

			// Act
			var result = await repository.UpdateAsync(entity);

			// Assert
			result.Name.ShouldBe("Updated Name");

			var updated = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
			updated.ShouldNotBeNull();
			updated.Name.ShouldBe("Updated Name");
		}

		[Fact]
		public async Task If_TenantIdDoesNotMatch_ThenThrowsException()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();

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
			await Should.ThrowAsync<TenantIsolationViolationException>(
				() => repository.UpdateAsync(entity));
		}

		[Fact]
		public async Task If_EntityIdNotFound_ThenThrowsException()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();

			var tenantId = Guid.NewGuid();
			scope.SetTenantContext(tenantId);
			var nonExistentEntity = new TestEntity
			{
				Id = Guid.NewGuid(),
				TenantId = tenantId,
				Name = "Non-existent Entity"
			};

			// Act & Assert
			await Should.ThrowAsync<TenantIsolationViolationException>(
				() => repository.UpdateAsync(nonExistentEntity));
		}

		[Fact]
		public async Task If_NonExistentEntitiesRange_ThenThrowsException()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenantId = Guid.NewGuid();
			scope.SetTenantContext(tenantId);

			var existingEntity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Existing" };
			context.TestEntities.Add(existingEntity);
			await context.SaveChangesAsync();
			context.ChangeTracker.Clear();

			var nonExistentEntity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Non-existent" };
			var entities = new[] { existingEntity, nonExistentEntity };

			// Act & Assert
			await Should.ThrowAsync<TenantIsolationViolationException>(
				() => repository.UpdateRangeAsync(entities));
		}

		[Fact]
		public async Task If_ValidEntitiesRange_UpdatesAllSuccessfully()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenantId = Guid.NewGuid();
			scope.SetTenantContext(tenantId);
			var entities = new[]
			{
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Original 1" },
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Original 2" }
		};
			context.TestEntities.AddRange(entities);
			await context.SaveChangesAsync();
			context.ChangeTracker.Clear();

			entities[0].Name = "Updated 1";
			entities[1].Name = "Updated 2";

			// Act
			var result = await repository.UpdateRangeAsync(entities);

			// Assert
			result.Count.ShouldBe(2);
			result[0].Name.ShouldBe("Updated 1");
			result[1].Name.ShouldBe("Updated 2");
		}

	}

	public class GetByIdsAsync(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
	{
		[Fact]
		public async Task WithValidId_ReturnsEntity()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenantId = Guid.NewGuid();
			scope.SetTenantContext(tenantId);
			var entity = new TestEntity
			{
				Id = Guid.NewGuid(),
				TenantId = tenantId,
				Name = "Test Entity"
			};
			context.TestEntities.Add(entity);
			await context.SaveChangesAsync();

			// Act
			var result = await repository.GetByIdAsync(entity.Id);

			// Assert
			result.ShouldNotBeNull();
			result.Id.ShouldBe(entity.Id);
			result.TenantId.ShouldBe(tenantId);
		}

		[Fact]
		public async Task WithDifferentTenant_ReturnsNull()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenant1Id = Guid.NewGuid();
			var tenant2Id = Guid.NewGuid();

			scope.SetTenantContext(tenant1Id);
			var entity = new TestEntity
			{
				Id = Guid.NewGuid(),
				TenantId = tenant1Id,
				Name = "Other Tenant Entity"
			};
			context.TestEntities.Add(entity);
			await context.SaveChangesAsync();

			// Act
			scope.SetTenantContext(tenant2Id);
			var result = await repository.GetByIdAsync(entity.Id);

			// Assert
			result.ShouldBeNull();
		}
		[Fact]
        public async Task WithValidIds_ReturnsEntities()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();
            var context = scope.GetDbContext();

            var tenantId = Guid.NewGuid();
            scope.SetTenantContext(tenantId);
            var entities = new[]
            {
            new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Entity 1" },
            new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Entity 2" },
            new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Entity 3" }
        };
            context.TestEntities.AddRange(entities);
            await context.SaveChangesAsync();

            var idsToGet = new[] { entities[0].Id, entities[2].Id };

            // Act
            var result = await repository.GetByIdsAsync(idsToGet);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);
            result.ShouldAllBe(e => e.TenantId == tenantId);
        }

        [Fact]
        public async Task WithDifferentTenantIds_ReturnsOnlyCurrentTenantEntities()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();
            var context = scope.GetDbContext();

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

            var idsToGet = new[] { tenant1Entity.Id, tenant2Entity.Id };

            // Act
            scope.SetTenantContext(tenant1Id);
            var result = await repository.GetByIdsAsync(idsToGet);

            // Assert
            result.Count.ShouldBe(1);
            result[0].Id.ShouldBe(tenant1Entity.Id);
            result[0].TenantId.ShouldBe(tenant1Id);
        }

		[Fact]
		public async Task GetAllAsync_ReturnsOnlyCurrentTenantEntities()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenant1Id = Guid.NewGuid();
			var tenant2Id = Guid.NewGuid();

			scope.SetTenantContext(tenant1Id);
			var tenant1Entities = new[]
			{
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Entity 1" },
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Entity 2" }
		};

			context.TestEntities.AddRange(tenant1Entities);
			await context.SaveChangesAsync();

			scope.SetTenantContext(tenant2Id);
			var tenant2Entity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenant2Id, Name = "Other Tenant" };
			context.TestEntities.Add(tenant2Entity);
			await context.SaveChangesAsync();

			// Act
			scope.SetTenantContext(tenant1Id);
			var result = await repository.GetAllAsync();

			// Assert
			result.ShouldNotBeNull();
			result.Count.ShouldBe(2);
			result.ShouldAllBe(e => e.TenantId == tenant1Id);
		}
	}

    public class FindSingleAsync_And_FindAsync(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
	{
        [Fact]
        public async Task If_MatchingPredicate_ThenReturnsEntityAsResult()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();
            var context = scope.GetDbContext();

            var tenantId = Guid.NewGuid();
            scope.SetTenantContext(tenantId);
            var entities = new[]
            {
            new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Unique Entity", IsActive = true },
            new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Other Entity", IsActive = false }
        };
            context.TestEntities.AddRange(entities);
            await context.SaveChangesAsync();

            // Act
            var result = await repository.FindSingleAsync(e => e.Name == "Unique Entity");

            // Assert
            result.ShouldNotBeNull();
            result.Name.ShouldBe("Unique Entity");
            result.TenantId.ShouldBe(tenantId);
        }

        [Fact]
        public async Task Otherwise_ReturnsNull()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();
            var context = scope.GetDbContext();

            var tenantId = Guid.NewGuid();
            scope.SetTenantContext(tenantId);
            var entity = new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Some Entity" };
            context.TestEntities.Add(entity);
            await context.SaveChangesAsync();

            // Act
            var result = await repository.FindSingleAsync(e => e.Name == "Non-existent Entity");

            // Assert
            result.ShouldBeNull();
        }

		[Fact]
		public async Task If_PredicateFilter_ThenReturnsFilteredResults()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenantId = Guid.NewGuid();
			scope.SetTenantContext(tenantId);
			var entities = new[]
			{
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Active Entity", IsActive = true },
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Inactive Entity", IsActive = false }
		};
			context.TestEntities.AddRange(entities);
			await context.SaveChangesAsync();

			// Act
			var result = await repository.FindAsync(e => e.IsActive);

			// Assert
			result.Count.ShouldBe(1);
			result[0].Name.ShouldBe("Active Entity");
		}
	}

    public class DeleteAsync_And_DeleteRangeAnsync(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
	{
        [Fact]
        public async Task If_EntityExists_ThenDeletesSuccessfully()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();
            var context = scope.GetDbContext();

            var tenantId = Guid.NewGuid();
            scope.SetTenantContext(tenantId);
            var entity = new TestEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "To Delete"
            };
            context.TestEntities.Add(entity);
            await context.SaveChangesAsync();
            context.Entry(entity).State = EntityState.Detached;

            // Act
            var result = await repository.DeleteAsync(entity);

            // Assert
            result.ShouldNotBeNull();
            result.Id.ShouldBe(entity.Id);

            var deleted = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
            deleted.ShouldBeNull();
        }

        [Fact]
        public async Task If_TenantEntityId_DoesNotMatchTenantContext_ThenThrowsException()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();
            var context = scope.GetDbContext();

            var tenant1Id = Guid.NewGuid();
            var tenant2Id = Guid.NewGuid();

            scope.SetTenantContext(tenant1Id);
            var entity = new TestEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenant1Id,
                Name = "Other Tenant Entity"
            };
            context.TestEntities.Add(entity);
            await context.SaveChangesAsync();

            // Act & Assert
            scope.SetTenantContext(tenant2Id);
            entity.TenantId = tenant2Id; // Simulate a spoofed entity
            await Should.ThrowAsync<TenantIsolationViolationException>(
                () => repository.DeleteAsync(entity));
        }

        [Fact]
        public async Task If_EntityIdNotFound_ThenReturnsFalse()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();

            var tenantId = Guid.NewGuid();
            scope.SetTenantContext(tenantId);
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await repository.DeleteAsync(nonExistentId);

            // Assert
            result.ShouldBeFalse();
        }

		[Fact]
		public async Task If_EntityIdAndTenantIdFoundAndMatch_ThenDeletesSuccessfully()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenantId = Guid.NewGuid();
			scope.SetTenantContext(tenantId);
			var entity = new TestEntity
			{
				Id = Guid.NewGuid(),
				TenantId = tenantId,
				Name = "To Delete"
			};
			context.TestEntities.Add(entity);
			await context.SaveChangesAsync();

			// Act
			var result = await repository.DeleteAsync(entity.Id);

			// Assert
			result.ShouldBeTrue();

			var deleted = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
			deleted.ShouldBeNull();
		}

		[Fact]
		public async Task If_ValidEntitiesRange_ThenDeletesAllSuccessfully()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenantId = Guid.NewGuid();
			scope.SetTenantContext(tenantId);
			var entities = new[]
			{
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "To Delete 1" },
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenantId, Name = "To Delete 2" }
		};
			context.TestEntities.AddRange(entities);
			await context.SaveChangesAsync();

			// Act
			var result = await repository.DeleteRangeAsync(entities);

			// Assert
			result.Count.ShouldBe(2);

			var remaining = await context.TestEntities.Where(e => entities.Select(x => x.Id).Contains(e.Id)).ToListAsync();
			remaining.ShouldBeEmpty();
		}

		[Fact]
        public async Task If_RangeHas_WrongTenantEntity_ThenThrowsException()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();
            var context = scope.GetDbContext();

            var tenant1Id = Guid.NewGuid();
            var tenant2Id = Guid.NewGuid();

            scope.SetTenantContext(tenant1Id);
            var entity1 = new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Entity 1" };
            context.TestEntities.Add(entity1);
            await context.SaveChangesAsync();

            scope.SetTenantContext(tenant2Id);
            var entity2 = new TestEntity { Id = Guid.NewGuid(), TenantId = tenant2Id, Name = "Entity 2" };
            context.TestEntities.Add(entity2);
            await context.SaveChangesAsync();

            // Act & Assert
            scope.SetTenantContext(tenant1Id);
            entity2.TenantId = tenant1Id; // Simulate spoofed entity
            await Should.ThrowAsync<TenantIsolationViolationException>(
                () => repository.DeleteRangeAsync(new[] { entity1, entity2 }));
        }
    }

    public class SystemContext(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
	{
        [Fact]
        public async Task If_EntityHasNoTenantIdValue_ThenWillNotAdd()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();

            scope.SetSystemContext();
            var entityWithoutTenantId = new TestEntity
            {
                Id = Guid.NewGuid(),
                // TenantId = Guid.Empty - default value
                Name = "Entity without TenantId"
            };

            // Act & Assert
            await Should.ThrowAsync<TenantIsolationViolationException>(
                () => repository.AddAsync(entityWithoutTenantId));
        }

        [Fact]
        public async Task ShouldBeAbleToUpdateAnyEntity()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();
            var context = scope.GetDbContext();

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
            context.Entry(entity).State = EntityState.Detached;

            // Act
            scope.SetSystemContext();
            entity.Name = "Updated by System";
            var result = await repository.UpdateAsync(entity);

            // Assert
            result.Name.ShouldBe("Updated by System");

            var updated = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
            updated.ShouldNotBeNull();
            updated.Name.ShouldBe("Updated by System");
        }

        [Fact]
        public async Task ShouldBeAbleToDeleteAnyTenantEntity()
        {
            // Arrange
            using var scope = fixture.CreateScope();
            var repository = scope.GetRepository();
            var context = scope.GetDbContext();

            var tenantId = Guid.NewGuid();
            scope.SetTenantContext(tenantId);
            var entity = new TestEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "To Delete by System"
            };
            context.TestEntities.Add(entity);
            await context.SaveChangesAsync();
            context.Entry(entity).State = EntityState.Detached;

            // Act
            scope.SetSystemContext();
            var result = await repository.DeleteAsync(entity);

            // Assert
            result.ShouldNotBeNull();

            var deleted = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
            deleted.ShouldBeNull();
        }

		[Fact]
		public async Task ShouldBeAbleToAccessAnyTenant()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenant1Id = Guid.NewGuid();
			var tenant2Id = Guid.NewGuid();

			scope.SetSystemContext();
			var entities = new[]
			{
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Tenant1 Entity" },
			new TestEntity { Id = Guid.NewGuid(), TenantId = tenant2Id, Name = "Tenant2 Entity" }
		};
			context.TestEntities.AddRange(entities);
			await context.SaveChangesAsync();

			// Act
			var result = await repository.GetAllAsync();

			// Assert
			result.Count.ShouldBeGreaterThanOrEqualTo(2);
			result.Select(e => e.TenantId).Distinct().Count().ShouldBeGreaterThanOrEqualTo(2);
		}

		[Fact]
		public async Task ShouldBeAbleToAddEntityWithAnyTenantId()
		{
			// Arrange
			using var scope = fixture.CreateScope();
			var repository = scope.GetRepository();
			var context = scope.GetDbContext();

			var tenantId = Guid.NewGuid();
			scope.SetSystemContext();
			var entity = new TestEntity
			{
				Id = Guid.NewGuid(),
				TenantId = tenantId,
				Name = "System Added Entity"
			};

			// Act
			var result = await repository.AddAsync(entity);

			// Assert
			result.TenantId.ShouldBe(tenantId);

			var saved = await context.TestEntities.FirstOrDefaultAsync(e => e.Id == entity.Id);
			saved.ShouldNotBeNull();
			saved.TenantId.ShouldBe(tenantId);
		}
	}
}
