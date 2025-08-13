// MultiTenant.Enforcer.Core/CrossTenantOperationManager.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace MultiTenant.Enforcer.Core
{
    /// <summary>
    /// Attribute for methods that legitimately need cross-tenant access.
    /// Required by the analyzer for any method using ICrossTenantOperationManager.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class AllowCrossTenantAccessAttribute : Attribute
    {
        /// <summary>
        /// Justification for why this operation needs cross-tenant access.
        /// </summary>
        public string Justification { get; }

        /// <summary>
        /// Required roles or claims for this cross-tenant operation.
        /// </summary>
        public string[] RequiredRoles { get; }

        /// <summary>
        /// Creates an attribute allowing cross-tenant access.
        /// </summary>
        /// <param name="justification">Business justification for cross-tenant access</param>
        /// <param name="requiredRoles">Required roles for authorization</param>
        public AllowCrossTenantAccessAttribute(string justification, params string[] requiredRoles)
        {
            Justification = justification ?? throw new ArgumentNullException(nameof(justification));
            RequiredRoles = requiredRoles ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Interface for managing cross-tenant operations with proper authorization.
    /// </summary>
    public interface ICrossTenantOperationManager
    {
        /// <summary>
        /// Executes an operation in system context with cross-tenant access.
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="justification">Business justification for the operation</param>
        /// <returns>Result of the operation</returns>
        Task<T> ExecuteCrossTenantOperationAsync<T>(Func<Task<T>> operation, string justification);

        /// <summary>
        /// Executes an operation in system context with cross-tenant access.
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="justification">Business justification for the operation</param>
        Task ExecuteCrossTenantOperationAsync(Func<Task> operation, string justification);

        /// <summary>
        /// Begins a cross-tenant operation context. Disposable for automatic cleanup.
        /// </summary>
        /// <param name="justification">Business justification for the operation</param>
        /// <returns>Disposable context</returns>
        Task<IDisposable> BeginCrossTenantOperationAsync(string justification);
    }

    /// <summary>
    /// Service for managing cross-tenant operations with proper authorization and logging.
    /// </summary>
    public class CrossTenantOperationManager : ICrossTenantOperationManager
    {
        private readonly ITenantContextAccessor _tenantAccessor;
        private readonly ILogger<CrossTenantOperationManager> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CrossTenantOperationManager(
            ITenantContextAccessor tenantAccessor,
            ILogger<CrossTenantOperationManager> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public async Task<T> ExecuteCrossTenantOperationAsync<T>(Func<Task<T>> operation, string justification)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (string.IsNullOrWhiteSpace(justification)) 
                throw new ArgumentException("Justification is required", nameof(justification));

            var originalContext = _tenantAccessor.Current;
            var userEmail = GetCurrentUserEmail();
            var ipAddress = GetClientIpAddress();

            _logger.LogInformation("Beginning cross-tenant operation: {Justification} by user {User} from {IP}",
                justification, userEmail, ipAddress);

            // Set system context temporarily
            _tenantAccessor.SetContext(TenantContext.SystemContext($"Cross-tenant: {justification}"));

            try
            {
                var result = await operation();

                _logger.LogInformation("Completed cross-tenant operation: {Justification} by user {User}",
                    justification, userEmail);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed cross-tenant operation: {Justification} by user {User}",
                    justification, userEmail);
                throw;
            }
            finally
            {
                // Always restore original context
                _tenantAccessor.SetContext((TenantContext)originalContext);
            }
        }

        public async Task ExecuteCrossTenantOperationAsync(Func<Task> operation, string justification)
        {
            await ExecuteCrossTenantOperationAsync(async () =>
            {
                await operation();
                return true; // Dummy return value
            }, justification);
        }

        public async Task<IDisposable> BeginCrossTenantOperationAsync(string justification)
        {
            if (string.IsNullOrWhiteSpace(justification))
                throw new ArgumentException("Justification is required", nameof(justification));

            var originalContext = _tenantAccessor.Current;
            var userEmail = GetCurrentUserEmail();

            _logger.LogInformation("Beginning cross-tenant operation context: {Justification} by user {User}",
                justification, userEmail);

            _tenantAccessor.SetContext(TenantContext.SystemContext($"Cross-tenant: {justification}"));

            return new CrossTenantOperationContext(originalContext, _tenantAccessor, _logger, justification, userEmail);
        }

        private string GetCurrentUserEmail()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.Email)?.Value ??
                   user?.FindFirst("email")?.Value ??
                   "system";
        }

        private string GetClientIpAddress()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            return httpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private class CrossTenantOperationContext : IDisposable
        {
            private readonly ITenantContext _originalContext;
            private readonly ITenantContextAccessor _tenantAccessor;
            private readonly ILogger _logger;
            private readonly string _justification;
            private readonly string _userEmail;
            private bool _disposed;

            public CrossTenantOperationContext(
                ITenantContext originalContext,
                ITenantContextAccessor tenantAccessor,
                ILogger logger,
                string justification,
                string userEmail)
            {
                _originalContext = originalContext;
                _tenantAccessor = tenantAccessor;
                _logger = logger;
                _justification = justification;
                _userEmail = userEmail;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _tenantAccessor.SetContext((TenantContext)_originalContext);
                    
                    _logger.LogInformation("Completed cross-tenant operation context: {Justification} by user {User}",
                        _justification, _userEmail);
                    
                    _disposed = true;
                }
            }
        }
    }
}
