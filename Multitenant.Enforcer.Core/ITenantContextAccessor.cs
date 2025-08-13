using System;

namespace MultiTenant.Enforcer.Core
{
	public interface ITenantContextAccessor
    {
        /// <summary>
        /// Gets the current tenant context.
        /// Throws InvalidOperationException if no context is set.
        /// </summary>
        ITenantContext Current { get; }

        /// <summary>
        /// Sets the current tenant context.
        /// </summary>
        /// <param name="context">The tenant context to set</param>
        void SetContext(TenantContext context);
    }

	/// <summary>
	/// Scoped service that tracks the current tenant context for the request.
	/// </summary>
	public class TenantContextAccessor : ITenantContextAccessor
	{
		private TenantContext? _current;

		public ITenantContext Current => _current ??
			throw new InvalidOperationException(
				"No tenant context set. Did you forget to add the TenantContextMiddleware?");

		public void SetContext(TenantContext context)
		{
			_current = context ?? throw new ArgumentNullException(nameof(context));
		}
	}
}
