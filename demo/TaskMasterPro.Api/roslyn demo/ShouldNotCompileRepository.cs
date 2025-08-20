using Microsoft.EntityFrameworkCore;
using TaskMasterPro.Api.Entities;

namespace TaskMasterPro.Api.CodeAnalysisDemo;

// File contains classes that were intentionally designed to not compile to test Roslyn analyzer behavior
// Uncomment to experience the compilation errors and warnings from the custom Roslyn analyzers
// ERRORS EXPECTED:
//		MTI001 - Direct DbSet access - Compilation error
//		MTI003 - Potential filter bypasses - Warning
//		MTI004 - Entities without repositories - Compilation error
//		MTI006 - DbContext not derived from TenantDbContext - Compilation error

public class ShouldNotCompileRepository(ShouldNotCompileDBContext context)
{
	// This method is intentionally designed to not compile to test Roslyn analyzer behavior
	// ERRORS EXPECTED: Either MTI001 or MTI004
	public async Task AddAsync(Project project)
	{
		context.Projects.Add(project);
		await context.SaveChangesAsync();
	}

	// This method is intentionally designed to not compile to test Roslyn analyzer behavior
	// ERRORS EXPECTED: Either MTI001 or MTI004
	public async Task<Project?> GetByIdAsync(Guid id)
	{
		return await context.Projects.FirstOrDefaultAsync(p => p.Id == id);
	}

	// This method is intentionally designed to not compile to test Roslyn analyzer behavior
	// ERRORS EXPECTED: Either MTI001 or MTI004
	public async Task<List<Project>> GetAllAsync()
	{
		return await context.Projects.ToListAsync();
	}
}
