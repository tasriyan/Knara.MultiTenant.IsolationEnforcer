using TaskMasterPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MultiTenant.Enforcer.AspNetCore;
using MultiTenant.Enforcer.EntityFramework;

var builder = WebApplication.CreateBuilder(args);

// Database configuration
builder.Services.AddDbContext<TaskMasterDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
		npgsqlOptions =>
		{
			npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
			npgsqlOptions.CommandTimeout(30);
		}));

// Multi-tenant isolation enforcer
builder.Services.AddMultiTenantIsolation<TaskMasterDbContext>(options =>
{
	options.DefaultTenantResolver = typeof(SubdomainTenantResolver);
	options.EnablePerformanceMonitoring = true;
	options.LogViolations = true;
});

// Authentication & Authorization
builder.Services.AddAuthentication("Bearer")
	.AddJwtBearer("Bearer", options =>
	{
		options.Authority = builder.Configuration["IdentityServer:Authority"];
		options.ApiName = "taskmaster_api";
		options.RequireHttpsMetadata = false; // Dev only
	});

builder.Services.AddAuthorization(options =>
{
	options.AddPolicy("ProjectManager", policy =>
		policy.RequireClaim("role", "ProjectManager", "Admin"));

	options.AddPolicy("SystemAdmin", policy =>
		policy.RequireClaim("role", "SystemAdmin"));
});

// Register repositories
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IAdminAuditLogRepository, AdminAuditLogRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// CRITICAL: Multi-tenant middleware must come before authentication
app.UseMultiTenantIsolation();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
	var context = scope.ServiceProvider.GetRequiredService<TaskMasterDbContext>();
	await context.Database.EnsureCreatedAsync();
}

app.Run();
