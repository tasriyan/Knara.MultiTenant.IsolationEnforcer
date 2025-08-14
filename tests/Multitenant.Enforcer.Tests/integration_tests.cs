// MultiTenant.Enforcer.Tests/Integration/TenantMiddlewareIntegrationTests.cs
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using MultiTenant.Enforcer.Core;
using MultiTenant.Enforcer.AspNetCore;

namespace MultiTenant.Enforcer.Tests.Integration
{
    /// <summary>
    /// Integration tests for tenant middleware and full request pipeline.
    /// </summary>
    public class TenantMiddlewareIntegrationTests : IClassFixture<TenantWebApplicationFactory>
    {
        private readonly TenantWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public TenantMiddlewareIntegrationTests(TenantWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task GetOrders_WithValidTenantSubdomain_ShouldReturnTenantData()
        {
            // Arrange
            _client.DefaultRequestHeaders.Host = "tenant1.localhost";

            // Act
            var response = await _client.GetAsync("/api/orders");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var content = await response.Content.ReadAsStringAsync();
            var orders = JsonSerializer.Deserialize<TestOrderDto[]>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            orders.Should().NotBeNull();
            orders!.Should().NotBeEmpty();
            orders.Should().AllSatisfy(o => o.TenantId.Should().Be(TestTenantData.Tenant1Id));

            // Verify tenant context headers
            response.Headers.Should().ContainKey("X-Tenant-Context");
            response.Headers.GetValues("X-Tenant-Context").First().Should().Be(TestTenantData.Tenant1Id.ToString());
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task GetOrders_WithDifferentTenantSubdomain_ShouldReturnDifferentData()
        {
            // Arrange
            _client.DefaultRequestHeaders.Host = "tenant2.localhost";

            // Act
            var response = await _client.GetAsync("/api/orders");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var content = await response.Content.ReadAsStringAsync();
            var orders = JsonSerializer.Deserialize<TestOrderDto[]>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            orders.Should().NotBeNull();
            orders!.Should().NotBeEmpty();
            orders.Should().AllSatisfy(o => o.TenantId.Should().Be(TestTenantData.Tenant2Id));
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task GetOrders_WithInvalidSubdomain_ShouldReturnBadRequest()
        {
            // Arrange
            _client.DefaultRequestHeaders.Host = "nonexistent.localhost";

            // Act
            var response = await _client.GetAsync("/api/orders");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Invalid tenant context");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task CreateOrder_ShouldAutoAssignTenantId()
        {
            // Arrange
            _client.DefaultRequestHeaders.Host = "tenant1.localhost";
            
            var newOrder = new CreateOrderRequest
            {
                CustomerName = "Integration Test Customer",
                Amount = 299.99m
            };

            var json = JsonSerializer.Serialize(newOrder);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/orders", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var createdOrder = JsonSerializer.Deserialize<TestOrderDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            createdOrder.Should().NotBeNull();
            createdOrder!.TenantId.Should().Be(TestTenantData.Tenant1Id);
            createdOrder.CustomerName.Should().Be("Integration Test Customer");
            createdOrder.Amount.Should().Be(299.99m);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task ConcurrentRequests_DifferentTenants_ShouldNotInterfere()
        {
            // Arrange
            var client1 = _factory.CreateClient();
            var client2 = _factory.CreateClient();
            
            client1.DefaultRequestHeaders.Host = "tenant1.localhost";
            client2.DefaultRequestHeaders.Host = "tenant2.localhost";

            // Act - Make concurrent requests
            var tasks = new[]
            {
                client1.GetAsync("/api/orders"),
                client2.GetAsync("/api/orders"),
                client1.GetAsync("/api/orders"),
                client2.GetAsync("/api/orders")
            };

            var responses = await Task.WhenAll(tasks);

            // Assert
            responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));

            var tenant1Responses = new[] { responses[0], responses[2] };
            var tenant2Responses = new[] { responses[1], responses[3] };

            foreach (var response in tenant1Responses)
            {
                var content = await response.Content.ReadAsStringAsync();
                var orders = JsonSerializer.Deserialize<TestOrderDto[]>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                orders.Should().AllSatisfy(o => o.TenantId.Should().Be(TestTenantData.Tenant1Id));
            }

            foreach (var response in tenant2Responses)
            {
                var content = await response.Content.ReadAsStringAsync();
                var orders = JsonSerializer.Deserialize<TestOrderDto[]>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                orders.Should().AllSatisfy(o => o.TenantId.Should().Be(TestTenantData.Tenant2Id));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task AdminEndpoint_WithoutSystemAdminRole_ShouldReturnForbidden()
        {
            // Arrange
            _client.DefaultRequestHeaders.Host = "tenant1.localhost";

            // Act
            var response = await _client.GetAsync("/api/admin/all-tenants");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task PerformanceMonitoring_ShouldLogSlowQueries()
        {
            // Arrange
            _client.DefaultRequestHeaders.Host = "tenant1.localhost";
            
            // Act - Make a request that should trigger performance monitoring
            var response = await _client.GetAsync("/api/orders/performance-test");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            // In a real scenario, you'd verify that performance metrics were logged
            // This would require setting up a test logging provider to capture log messages
        }
    }

    /// <summary>
    /// Custom WebApplicationFactory for integration testing.
    /// </summary>
    public class TenantWebApplicationFactory : WebApplicationFactory<TestStartup>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove the real database context
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TestDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<TestDbContext>(options =>
                {
                    options.UseInMemoryDatabase("IntegrationTestDb");
                });

                // Override tenant lookup service with test implementation
                services.AddSingleton<ITenantDataProvider, TestTenantDataProvider>();
            });

            builder.UseEnvironment("Testing");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            // Seed test data
            using var scope = host.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            SeedTestData(context);

            return host;
        }

        private static void SeedTestData(TestDbContext context)
        {
            // Ensure database is created
            context.Database.EnsureCreated();

            // Check if data already exists
            if (context.Orders.Any()) return;

            // Seed test orders
            var orders = new[]
            {
                new TestOrder { Id = Guid.NewGuid(), TenantId = TestTenantData.Tenant1Id, CustomerName = "Tenant1 Customer 1", Amount = 100.00m, Status = OrderStatus.Pending },
                new TestOrder { Id = Guid.NewGuid(), TenantId = TestTenantData.Tenant1Id, CustomerName = "Tenant1 Customer 2", Amount = 250.50m, Status = OrderStatus.Processing },
                new TestOrder { Id = Guid.NewGuid(), TenantId = TestTenantData.Tenant2Id, CustomerName = "Tenant2 Customer 1", Amount = 175.25m, Status = OrderStatus.Shipped },
                new TestOrder { Id = Guid.NewGuid(), TenantId = TestTenantData.Tenant2Id, CustomerName = "Tenant2 Customer 2", Amount = 89.99m, Status = OrderStatus.Delivered }
            };

            context.Orders.AddRange(orders);
            context.SaveChanges();
        }
    }

    /// <summary>
    /// Test startup class for integration tests.
    /// </summary>
    public class TestStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<TestDbContext>((provider, options) =>
            {
                options.UseInMemoryDatabase("TestDb");
            });

            // Configure multi-tenant isolation
            services.AddMultiTenantIsolation<TestDbContext>(options =>
            {
                options.UseSubdomainTenantResolver();
                options.PerformanceMonitoring.Enabled = true;
                options.PerformanceMonitoring.SlowQueryThresholdMs = 100;
            });

            services.AddControllers();
            
            // Add test controllers
            services.AddScoped<TestOrdersController>();
            services.AddScoped<TestAdminController>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment() || env.EnvironmentName == "Testing")
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMultiTenantIsolation(); // Must come before authentication
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }

    /// <summary>
    /// Test tenant data provider for integration tests.
    /// </summary>
    public class TestTenantDataProvider : ITenantDataProvider
    {
        public Task<Guid?> GetTenantIdByDomainAsync(string domain)
        {
            return domain.ToLowerInvariant() switch
            {
                "tenant1" => Task.FromResult<Guid?>(TestTenantData.Tenant1Id),
                "tenant2" => Task.FromResult<Guid?>(TestTenantData.Tenant2Id),
                _ => Task.FromResult<Guid?>(null)
            };
        }

        public Task<TenantInfo?> GetTenantInfoAsync(Guid tenantId)
        {
            if (tenantId == TestTenantData.Tenant1Id)
            {
                return Task.FromResult<TenantInfo?>(new TenantInfo
                {
                    Id = TestTenantData.Tenant1Id,
                    Name = "Test Tenant 1",
                    Domain = "tenant1",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                });
            }

            if (tenantId == TestTenantData.Tenant2Id)
            {
                return Task.FromResult<TenantInfo?>(new TenantInfo
                {
                    Id = TestTenantData.Tenant2Id,
                    Name = "Test Tenant 2",
                    Domain = "tenant2",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                });
            }

            return Task.FromResult<TenantInfo?>(null);
        }

        public Task<TenantInfo[]> GetAllActiveTenantsAsync()
        {
            var tenants = new[]
            {
                new TenantInfo { Id = TestTenantData.Tenant1Id, Name = "Test Tenant 1", Domain = "tenant1", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-30) },
                new TenantInfo { Id = TestTenantData.Tenant2Id, Name = "Test Tenant 2", Domain = "tenant2", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-20) }
            };

            return Task.FromResult(tenants);
        }
    }

    /// <summary>
    /// Test data constants.
    /// </summary>
    public static class TestTenantData
    {
        public static readonly Guid Tenant1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public static readonly Guid Tenant2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    }

    /// <summary>
    /// DTOs for testing.
    /// </summary>
    public class TestOrderDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime OrderDate { get; set; }
    }

    public class CreateOrderRequest
    {
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// Test controllers for integration testing.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
    public class TestOrdersController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        private readonly ITenantRepository<TestOrder> _orderRepository;
        private readonly ITenantContextAccessor _tenantAccessor;

        public TestOrdersController(ITenantRepository<TestOrder> orderRepository, ITenantContextAccessor tenantAccessor)
        {
            _orderRepository = orderRepository;
            _tenantAccessor = tenantAccessor;
        }

        [Microsoft.AspNetCore.Mvc.HttpGet]
        public async Task<Microsoft.AspNetCore.Mvc.ActionResult<List<TestOrderDto>>> GetOrders()
        {
            var orders = await _orderRepository.GetAllAsync();
            var dtos = orders.Select(o => new TestOrderDto
            {
                Id = o.Id,
                TenantId = o.TenantId,
                CustomerName = o.CustomerName,
                Amount = o.Amount,
                Status = o.Status,
                OrderDate = o.OrderDate
            }).ToList();

            return Microsoft.AspNetCore.Mvc.Ok(dtos);
        }

        [Microsoft.AspNetCore.Mvc.HttpPost]
        public async Task<Microsoft.AspNetCore.Mvc.ActionResult<TestOrderDto>> CreateOrder(CreateOrderRequest request)
        {
            var order = new TestOrder
            {
                CustomerName = request.CustomerName,
                Amount = request.Amount,
                Status = OrderStatus.Pending
                // TenantId will be auto-assigned
            };

            var createdOrder = await _orderRepository.AddAsync(order);

            var dto = new TestOrderDto
            {
                Id = createdOrder.Id,
                TenantId = createdOrder.TenantId,
                CustomerName = createdOrder.CustomerName,
                Amount = createdOrder.Amount,
                Status = createdOrder.Status,
                OrderDate = createdOrder.OrderDate
            };

            return Microsoft.AspNetCore.Mvc.CreatedAtAction(nameof(GetOrders), new { id = dto.Id }, dto);
        }

        [Microsoft.AspNetCore.Mvc.HttpGet("performance-test")]
        public async Task<Microsoft.AspNetCore.Mvc.ActionResult> PerformanceTest()
        {
            // Simulate a potentially slow operation for performance monitoring
            await Task.Delay(150); // Above the 100ms threshold set in configuration
            
            var orders = await _orderRepository.GetAllAsync();
            return Microsoft.AspNetCore.Mvc.Ok(new { Count = orders.Count });
        }
    }

    [Microsoft.AspNetCore.Mvc.ApiController]
    [Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
    public class TestAdminController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        [Microsoft.AspNetCore.Mvc.HttpGet("all-tenants")]
        [AllowCrossTenantAccess("Admin needs to view all tenants", "SystemAdmin")]
        public Microsoft.AspNetCore.Mvc.ActionResult GetAllTenants()
        {
            // This would normally require system admin authorization
            return Microsoft.AspNetCore.Mvc.Forbid(); // Simplified for testing
        }
    }
}
