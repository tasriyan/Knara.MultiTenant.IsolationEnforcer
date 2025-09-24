using Knara.MultiTenant.IsolationEnforcer.AspNetCore.Middleware;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TaskMasterPro.Api;
using TaskMasterPro.Api.DataAccess;
using TaskMasterPro.Api.Shared;


var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration(builder.Configuration)
	.Enrich.WithProperty("ApplicationName", "TaskMasterPro.Api")
	.CreateLogger();

// Add Serilog to the application
builder.Host.UseSerilog();

// Debugging code to verify configuration loading - not for production use
builder.LogConfigurationValues();

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

//setup database contexts
builder.ConfigureEntityFramework();

// Add multi-tenant enforcer services
builder.AddMultiTenantEnforcer();

// Register features endpoints
builder.Services.AddTaskMasterProServices(builder.Configuration);
builder.Services.RegisterTaskMasterProEndpoints();

var app = builder.Build();

app.EnsureDatabaseCreated();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.LogUserClaims();			// Debugging middleware to log claims - not for production use					
app.UseMultiTenantIsolation();  // Multi-tenant middleware ensures isolation based on tenant resolution

app.MapTaskMasterProEndpoints();

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


