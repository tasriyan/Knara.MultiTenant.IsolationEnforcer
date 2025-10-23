# TaskMasterPro.Api Demo

A working ASP.NET Core Web API demonstrating multi-tenant isolation in a project management application where multiple companies share the same database with complete data isolation.

## What This Demonstrates

- **Compile-time safety** - Roslyn analyzers prevent unsafe code from building
- **Two isolation approaches** - TenantIsolatedDbContext (automatic) and TenantIsolatedRepository (explicit)
- **Cross-tenant operations** - Admin endpoints with proper authorization and auditing
- **JWT tenant resolution** - Extract tenant from authentication token claims
- **Commented anti-patterns** - Code that triggers analyzer errors

## Infrastructure Dependencies

**Database**: SQLite with shared schema, row-level tenant isolation via `TenantId` column

**Tenant Store**: `TaskMasterProTenantStore` reads tenant info from `Company` table

**Cache**: In-memory cache with 30-minute expiration for tenant resolution

**Authentication**: JWT Bearer tokens with required claims:
- `tenant_id` or `allowed_tenants` - Identifies which tenant(s) user belongs to
- `scope: taskmasterpro-api` - Required for API access
- `role: SystemAdmin` - Required for cross-tenant admin operations

**Tenant Resolution**: JWT strategy extracts tenant from token claims (`tenant_id` or `allowed_tenants`)

**Performance Monitoring**: Tracks queries exceeding 500ms, logs violations and cross-tenant operations

## Domain Model

**Tenant-Isolated** (implement `ITenantIsolated`):
- User, Project, ProjectTask, TimeEntry

**Non-Tenant** (shared across all tenants):
- Company (tenant metadata), AdminAuditLog (cross-tenant audit trail)

## Three DbContext Implementations

**TaskMasterDbContext** - Extends `TenantIsolatedDbContext`, automatic global query filters. Recommended approach.

**UnsafeDbContext** - Regular `DbContext` without automatic filtering. Safe only when used through `TenantIsolatedRepository<T>`.

**NotTenantIsolatedAdminDbContext** - For non-tenant entities. Compiles safely because it doesn't access tenant-isolated entities.

## Repository Patterns

**Pattern 1**: `TenantIsolatedRepository<Project, UnsafeDbContext>`
- Works with any DbContext
- Adds explicit WHERE clauses for tenant filtering

**Pattern 2**: Direct DbContext usage with `TaskMasterDbContext`
- Works because context has global query filters
- Cleaner code, less boilerplate

Both patterns are compile-time safe.

## Endpoints

**Project Management** (tenant-isolated):
- `GET /api/projects/{filter}` - List projects for current tenant
- `GET /api/projects/{id}` - Get project (404 if wrong tenant)
- `POST /api/projects` - Create project (TenantId auto-assigned)

**Admin Operations** (cross-tenant with `[AllowCrossTenantAccess]`):
- `GET /api/admin/companies` - List all tenants
- `GET /api/admin/audit-logs` - View audit logs across tenants
- `GET /api/admin/tenant-statistics` - Aggregate stats per tenant
- `POST /api/admin/migrate-user` - Move user between tenants

All admin endpoints require SystemAdmin role and execute via `ICrossTenantOperationManager.ExecuteCrossTenantOperationAsync()`.

## Commented-Out Code (Analyzer Demonstrations)

**In `Features/Projects/DataAccess.cs`**:
Entire `UnsafeProjectRepository` class showing direct DbSet access on tenant-isolated entities without proper safeguards. Uncomment to see **MTI001** or **MTI004** compiler errors.

**In `CreateProject.cs` and `GetProject.cs`**:
Bad example methods showing direct `_context.Set<Project>().ToListAsync()` that would trigger **MTI001** errors.

**In `GetProjects.cs`**:
Example endpoint using `UnsafeDbContext` directly, demonstrating what the analyzers prevent.

**Purpose**: Show developers exactly what mistakes the library catches at compile time.

## Running the Demo

1. .NET 8.0 or later
2. No external dependencies - SQLite database auto-created on startup
3. Configure JWT authentication with appropriate claims
4. Database seeded automatically via `TaskMasterDbSeed.SeedData()`

Development helpers (enabled only in Development environment):
- `builder.LogConfigurationValues()` - Logs startup configuration
- `app.LogUserClaims()` - Logs JWT claims for each request

## Testing Scenarios

**Normal User**: Authenticate with `tenant_id` claim, create/list projects - see only your tenant's data.

**Admin User**: Authenticate with SystemAdmin role, access `/api/admin/*` endpoints - see all tenants.

**Analyzer Testing**: Uncomment `UnsafeProjectRepository` class and build - see MTI001/MTI004 errors.

## Additional Resources

- [Main Library Documentation](../../README.md)
- [Configuration Guide](../../docs/configuration.md)
- [Features Overview](../../docs/features.md)
- [Tenant Resolvers Guide](../../docs/resolvers.md)
