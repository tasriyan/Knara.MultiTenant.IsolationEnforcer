namespace Multitenant.Enforcer.Core;

    /// <summary>
    /// Base exception for all multi-tenant isolation related errors.
    /// </summary>
    public abstract class MultiTenantException : Exception
    {
        protected MultiTenantException(string message) : base(message) { }
        protected MultiTenantException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when a tenant isolation violation is detected at runtime.
    /// This indicates a serious security issue that must be addressed immediately.
    /// </summary>
    public class TenantIsolationViolationException : MultiTenantException
    {
        /// <summary>
        /// The tenant ID that was expected.
        /// </summary>
        public Guid ExpectedTenantId { get; }

        /// <summary>
        /// The tenant ID that was actually found (if applicable).
        /// </summary>
        public Guid? ActualTenantId { get; }

        /// <summary>
        /// The type of entity that caused the violation.
        /// </summary>
        public string? EntityType { get; }

        /// <summary>
        /// The ID of the entity that caused the violation.
        /// </summary>
        public Guid? EntityId { get; }

        public TenantIsolationViolationException(string message) : base(message)
        {
        }

        public TenantIsolationViolationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public TenantIsolationViolationException(
            string message,
            Guid expectedTenantId,
            Guid? actualTenantId = null,
            string? entityType = null,
            Guid? entityId = null) : base(message)
        {
            ExpectedTenantId = expectedTenantId;
            ActualTenantId = actualTenantId;
            EntityType = entityType;
            EntityId = entityId;
        }
    }

    /// <summary>
    /// Exception thrown when tenant resolution fails (e.g., invalid subdomain, missing JWT claim).
    /// </summary>
    public class TenantResolutionException : MultiTenantException
    {
        /// <summary>
        /// The attempted tenant identifier that failed to resolve.
        /// </summary>
        public string? AttemptedTenantIdentifier { get; }

        /// <summary>
        /// The method used to attempt tenant resolution (e.g., "Subdomain", "JWT", "Header").
        /// </summary>
        public string? ResolutionMethod { get; }

        public TenantResolutionException(string message) : base(message)
        {
        }

        public TenantResolutionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public TenantResolutionException(
            string message,
            string? attemptedTenantIdentifier = null,
            string? resolutionMethod = null) : base(message)
        {
            AttemptedTenantIdentifier = attemptedTenantIdentifier;
            ResolutionMethod = resolutionMethod;
        }
    }

    /// <summary>
    /// Exception thrown when cross-tenant operations are attempted without proper authorization.
    /// </summary>
    public class UnauthorizedCrossTenantAccessException : MultiTenantException
    {
        /// <summary>
        /// The operation that was attempted.
        /// </summary>
        public string? AttemptedOperation { get; }

        /// <summary>
        /// The user who attempted the operation.
        /// </summary>
        public string? UserId { get; }

        /// <summary>
        /// The required roles for the operation.
        /// </summary>
        public string[]? RequiredRoles { get; }

        public UnauthorizedCrossTenantAccessException(string message) : base(message)
        {
        }

        public UnauthorizedCrossTenantAccessException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public UnauthorizedCrossTenantAccessException(
            string message,
            string? attemptedOperation = null,
            string? userId = null,
            string[]? requiredRoles = null) : base(message)
        {
            AttemptedOperation = attemptedOperation;
            UserId = userId;
            RequiredRoles = requiredRoles;
        }
    }

    /// <summary>
    /// Exception thrown when tenant configuration is invalid or missing.
    /// </summary>
    public class TenantConfigurationException : MultiTenantException
    {
        /// <summary>
        /// The configuration key that caused the issue.
        /// </summary>
        public string? ConfigurationKey { get; }

        public TenantConfigurationException(string message) : base(message)
        {
        }

        public TenantConfigurationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public TenantConfigurationException(string message, string? configurationKey) : base(message)
        {
            ConfigurationKey = configurationKey;
        }
    }
