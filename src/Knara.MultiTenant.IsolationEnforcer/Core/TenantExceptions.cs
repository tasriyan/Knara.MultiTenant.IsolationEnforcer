namespace Knara.MultiTenant.IsolationEnforcer.Core;

    public abstract class MultiTenantException : Exception
    {
        protected MultiTenantException(string message) : base(message) { }
        protected MultiTenantException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class TenantIsolationViolationException : MultiTenantException
    {
        public Guid ExpectedTenantId { get; }
        public Guid? ActualTenantId { get; }
        public string? EntityType { get; }
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

    public class TenantResolutionException : MultiTenantException
    {
        public string? AttemptedTenantIdentifier { get; }
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

    public class UnauthorizedCrossTenantAccessException : MultiTenantException
    {
        public string? AttemptedOperation { get; }
        public string? UserId { get; }
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

    public class TenantConfigurationException : MultiTenantException
    {
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
