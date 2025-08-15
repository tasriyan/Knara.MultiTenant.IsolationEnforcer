using TaskMasterPro.Api.Features.Projects;
using TaskMasterPro.Api.Features.Tasks;
using TaskMasterPro.Data;
using Multitenant.Enforcer;
using Multitenant.Enforcer.AspnetCore;
using Multitenant.Enforcer.Resolvers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database configuration - SQLite In-Memory
builder.Services.AddDbContext<TaskMasterDbContext>(options =>
	options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"), sqliteOptions =>
	{
		sqliteOptions.CommandTimeout(builder.Configuration.GetValue<int>("Database:CommandTimeout"));
	}));

// Multi-tenant isolation enforcer
builder.Services.AddMultiTenantIsolation<TaskMasterDbContext>(options =>
{
	options.DefaultTenantResolver = typeof(SubdomainTenantResolver);
	options.PerformanceMonitoring = new Multitenant.Enforcer.PerformanceMonitor.PerformanceMonitoringOptions
	{
		Enabled = true,
	};
	options.LogViolations = true;
});

// Authentication & Authorization
builder.Services.AddAuthentication("Bearer")
	.AddJwtBearer("Bearer", options =>
	{
		options.Authority = builder.Configuration["IdentityServer:Authority"];
		options.Audience = builder.Configuration["Authentication:Bearer:Audience"];
		options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("Authentication:Bearer:RequireHttpsMetadata");
	});

builder.Services.AddAuthorizationBuilder()
	.AddPolicy("ApiScope", policy =>
	{
		policy.RequireAuthenticatedUser();
		policy.RequireClaim("scope", "taskmasterpro-api");
	});

// Register repositories
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();


// CRITICAL: Multi-tenant middleware must come before authentication
app.UseMultiTenantIsolation();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
	var context = scope.ServiceProvider.GetRequiredService<TaskMasterDbContext>();
	
	// For in-memory SQLite, we need to open the connection to keep the database alive
	await context.Database.OpenConnectionAsync();
	await context.Database.EnsureCreatedAsync();
}

app.Run();
