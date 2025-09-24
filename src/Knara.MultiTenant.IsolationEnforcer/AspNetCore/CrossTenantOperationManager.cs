using Knara.MultiTenant.IsolationEnforcer.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Knara.MultiTenant.IsolationEnforcer.AspNetCore;

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


public class CrossTenantOperationManager(
		ITenantContextAccessor tenantAccessor,
		ILogger<CrossTenantOperationManager> logger,
		IHttpContextAccessor httpContextAccessor) : ICrossTenantOperationManager
{
	private readonly ITenantContextAccessor _tenantAccessor =  tenantAccessor ?? throw new (nameof(tenantAccessor));
	private readonly ILogger<CrossTenantOperationManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

	public async Task<T> ExecuteCrossTenantOperationAsync<T>(Func<Task<T>> operation, string justification)
	{
		if (operation is null)
			throw new ArgumentNullException(nameof(operation));
		if (string.IsNullOrWhiteSpace(justification))
			throw new ArgumentException("Justification cannot be null or whitespace.", nameof(justification));

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
			throw new ArgumentException("Justification cannot be null or whitespace.", nameof(justification));

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

	private class CrossTenantOperationContext(
		ITenantContext originalContext,
		ITenantContextAccessor tenantAccessor,
		ILogger logger,
		string justification,
		string userEmail) : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (!_disposed)
			{
				tenantAccessor.SetContext((TenantContext)originalContext);

				logger.LogInformation("Completed cross-tenant operation context: {Justification} by user {User}",
					justification, userEmail);

				_disposed = true;
			}
		}
	}
}
