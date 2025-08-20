using Microsoft.EntityFrameworkCore;
using Multitenant.Enforcer.AspnetCore;
using Serilog;
using TaskMasterPro.Api;
using TaskMasterPro.Api.Data;
using TaskMasterPro.Api.Features.Projects;
using TaskMasterPro.Api.Features.Tasks;
using TaskMasterPro.Api.Shared;


var builder = WebApplication.CreateBuilder(args);

// Configure Serilog from appsettings configuration
Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration(builder.Configuration)
	.Enrich.WithProperty("ApplicationName", "TaskMasterPro.Api")
	.CreateLogger();

// Add Serilog to the application
builder.Host.UseSerilog();

// Debugging code to verify configuration loading - not for production use
builder.LogConfigurationValues();

// Add multi-tenant enforcer services
builder.AddMultiTenantEnforcer();

// Configure api
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();

// Authentication & Authorization
builder.Services.AddAuthentication("Bearer").AddJwtBearer();
builder.Services.AddAuthorizationBuilder()
	.AddPolicy("ApiScope", policy =>
	{
		policy.RequireAuthenticatedUser();
		policy.RequireClaim("scope", "taskmasterpro-api");
	});


// Register features services
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.RegisterApplicationEndpoints();

var app = builder.Build();

// Ensure the database is created and seeded
app.EnsureDatabaseCreated();

// Configure request pipeline
app.UseHttpsRedirection();

// Configure authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

app.LogUserClaims();			// Debugging middleware to log claims - not for production use					
app.UseMultiTenantIsolation();  // Multi-tenant middleware must come before authorization

// Configure api endpoints
app.MapApplicationEndpoints();

app.Run();

static class DevelopmentExtensions
{
	// Log configuration values for troubleshooting
	public static WebApplicationBuilder LogConfigurationValues(this WebApplicationBuilder builder)
	{
		if (builder.Environment.IsDevelopment())
		{
			Log.Debug("Logging configuration values for development environment");

			Log.Debug("Environment: {Environment}", builder.Environment.EnvironmentName);
			Log.Debug("Log Level Default: {LogLevel}", builder.Configuration["Logging:LogLevel:Default"]);
			Log.Debug("JWT ValidIssuer: {ValidIssuer}", builder.Configuration["Authentication:Schemes:Bearer:ValidIssuer"]);
			Log.Debug("MultiTenant EnableViolationLogging: {EnableViolationLogging}", builder.Configuration["MultiTenant:EnableViolationLogging"]);
			Log.Debug("Connection String: {ConnectionString}", builder.Configuration.GetConnectionString("DefaultConnection"));
		}

		return builder;
	}

	// Ensure the TaskMasterPro database is created
	public static WebApplication EnsureDatabaseCreated(this WebApplication app)
	{
		try
		{
			Log.Information("Ensuring tasks master database is created");

			using var scope = app.Services.CreateScope();
			var scopedServices = scope.ServiceProvider;
			var context = scopedServices.GetRequiredService<TaskMasterDbContext>();
			context.Database.EnsureCreated();

			Log.Information("Tasks master database creation check completed");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "An error occurred while ensuring the database was created");
			throw;
		}
		return app;
	}

	// Debugging middleware to log claims
	public static WebApplication LogUserClaims(this WebApplication app)
	{
		if (app.Environment.IsDevelopment())
		{
			Log.Debug("Adding middleware to log user claims for debugging purposes");
			app.Use(async (context, next) =>
			{
				if (context.User.Identity?.IsAuthenticated == true)
				{
					Log.Debug("User authenticated: {UserName}", context.User.Identity.Name);
					foreach (var claim in context.User.Claims)
					{
						Log.Debug("Claim: {ClaimType} = {ClaimValue}", claim.Type, claim.Value);
					}
				}
				else
				{
					Log.Debug("User not authenticated");
				}
				await next();
			});
		}
		return app;
	}
}


