using Knara.MultiTenant.IsolationEnforcer.EntityFramework;
using Knara.MultiTenant.IsolationEnforcer.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace IntegrationTests;

public class RepositoryFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private IServiceProvider? _serviceProvider;

	public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

		// Create database schema once
		using var scope = _serviceProvider.CreateScope();
		using var context = scope.ServiceProvider.GetRequiredService<UnsafeTestDbContext>();
		await context.Database.EnsureCreatedAsync();

		using var tenantsContext = scope.ServiceProvider.GetRequiredService<TestTenantsStoreDbContext>();
		await tenantsContext.Database.EnsureCreatedAsync();
	}

	public async Task DisposeAsync()
	{
		if (_container != null)
		{
			await _container.DisposeAsync();
		}
	}

	private void ConfigureServices(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Database:CommandTimeout"] = "30"
            })
            .Build();

        services.AddLogging();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddDbContext<UnsafeTestDbContext>(options =>
            options.UseNpgsql(ConnectionString));

        services.AddDbContext<TestTenantsStoreDbContext>(options =>
            options.UseNpgsql(ConnectionString));

        services.AddMultiTenantIsolation(options =>
            {
                options.CacheTenantResolution = true;
                options.CacheExpirationMinutes = 30;
            })
            .WithInMemoryTenantCache()
            .WithTenantsStore<TestTenantStore>()
            .WithSubdomainResolutionStrategy(options =>
            {
                options.CacheMappings = true;
                options.ExcludedSubdomains = ["www", "api", "admin", "localhost"];
                options.SystemAdminClaimValue = "SystemAdmin";
            });

        services.AddScoped<ITenantIsolatedRepository<TestEntity>, TenantIsolatedRepository<TestEntity, UnsafeTestDbContext>>();
    }

	public IServiceScope CreateScope()
	{
		return _serviceProvider!.CreateScope();
	}
}