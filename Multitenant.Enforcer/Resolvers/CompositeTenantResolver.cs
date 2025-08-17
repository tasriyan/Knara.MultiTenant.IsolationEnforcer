using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Multitenant.Enforcer.Core;

namespace Multitenant.Enforcer.Resolvers;

public class CompositeTenantResolver(ITenantResolver[] resolvers, ILogger<CompositeTenantResolver> logger) : ITenantResolver
{
	private readonly ITenantResolver[] _resolvers = resolvers ?? throw new ArgumentNullException(nameof(resolvers));

	public async Task<TenantContext> ResolveTenantAsync(HttpContext context, CancellationToken cancellationToken)
	{
		var exceptions = new List<Exception>();

		foreach (var resolver in _resolvers)
		{
			try
			{
				var result = await resolver.ResolveTenantAsync(context, cancellationToken);
				logger.LogDebug("Tenant resolved using {ResolverType}: {TenantId}",
					resolver.GetType().Name, result.TenantId);
				return result;
			}
			catch (TenantResolutionException ex)
			{
				exceptions.Add(ex);
				logger.LogDebug("Tenant resolution failed with {ResolverType}: {Error}",
					resolver.GetType().Name, ex.Message);
			}
		}

		var errorMessage = $"All tenant resolution strategies failed. Tried: {string.Join(", ", _resolvers.Select(r => r.GetType().Name))}";
		logger.LogWarning("Failed to resolve tenant using any strategy");

		throw new TenantResolutionException(errorMessage, null, "Composite");
	}
}
