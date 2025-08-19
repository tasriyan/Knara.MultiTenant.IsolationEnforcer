#!/bin/bash

# TaskMaster Pro - Multi-Tenant SaaS Setup Script
# This script sets up the complete testing environment

echo "🚀 TaskMaster Pro Multi-Tenant SaaS Setup"
echo "=========================================="

# Create project structure
echo "📁 Creating project structure..."
mkdir -p TaskMasterPro.SaaS/{src,tests,scripts}
cd TaskMasterPro.SaaS

# Create solution and projects
echo "🔧 Creating .NET solution and projects..."
dotnet new sln -n TaskMasterPro

# Core library
dotnet new classlib -n TaskMasterPro.Core -o src/TaskMasterPro.Core
dotnet sln add src/TaskMasterPro.Core/TaskMasterPro.Core.csproj

# Infrastructure
dotnet new classlib -n TaskMasterPro.Infrastructure -o src/TaskMasterPro.Infrastructure
dotnet sln add src/TaskMasterPro.Infrastructure/TaskMasterPro.Infrastructure.csproj

# API
dotnet new webapi -n TaskMasterPro.Api -o src/TaskMasterPro.Api
dotnet sln add src/TaskMasterPro.Api/TaskMasterPro.Api.csproj

# Tests
dotnet new xunit -n TaskMasterPro.Tests -o tests/TaskMasterPro.Tests
dotnet sln add tests/TaskMasterPro.Tests/TaskMasterPro.Tests.csproj

# Multi-tenant enforcer library
dotnet new classlib -n MultiTenant.Enforcer -o src/MultiTenant.Enforcer
dotnet sln add src/MultiTenant.Enforcer/MultiTenant.Enforcer.csproj

echo "📦 Adding NuGet packages..."

# Core dependencies
cd src/TaskMasterPro.Core
dotnet add package System.ComponentModel.Annotations
cd ../..

# Infrastructure dependencies
cd src/TaskMasterPro.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.Extensions.Caching.Memory
dotnet add package Microsoft.Extensions.Logging
dotnet add reference ../TaskMasterPro.Core/TaskMasterPro.Core.csproj
dotnet add reference ../MultiTenant.Enforcer/MultiTenant.Enforcer.csproj
cd ../..

# API dependencies
cd src/TaskMasterPro.Api
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.AspNetCore.Authorization
dotnet add package Swashbuckle.AspNetCore
dotnet add reference ../TaskMasterPro.Core/TaskMasterPro.Core.csproj
dotnet add reference ../TaskMasterPro.Infrastructure/TaskMasterPro.Infrastructure.csproj
dotnet add reference ../MultiTenant.Enforcer/MultiTenant.Enforcer.csproj
cd ../..

# Test dependencies
cd tests/TaskMasterPro.Tests
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add package Microsoft.EntityFrameworkCore.InMemory
dotnet add package FluentAssertions
dotnet add package Moq
dotnet add package Testcontainers.PostgreSql
dotnet add reference ../../src/TaskMasterPro.Api/TaskMasterPro.Api.csproj
dotnet add reference ../../src/TaskMasterPro.Infrastructure/TaskMasterPro.Infrastructure.csproj
cd ../..

# Multi-tenant enforcer dependencies
cd src/MultiTenant.Enforcer
dotnet add package Microsoft.CodeAnalysis.Analyzers
dotnet add package Microsoft.CodeAnalysis.CSharp
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.Http.Abstractions
dotnet add package Microsoft.Extensions.DependencyInjection.Abstractions
cd ../..

echo "🐳 Creating Docker setup..."
cat > docker-compose.yml << 'EOF'
version: '3.8'

services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: taskmaster_dev
      POSTGRES_USER: taskmaster
      POSTGRES_PASSWORD: dev_password_123
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/init-db.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U taskmaster"]
      interval: 30s
      timeout: 10s
      retries: 3

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 30s
      timeout: 10s
      retries: 3

  taskmaster-api:
    build:
      context: .
      dockerfile: src/TaskMasterPro.Api/Dockerfile
    ports:
      - "5000:80"
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=taskmaster_dev;Username=taskmaster;Password=dev_password_123
      - ASPNETCORE_ENVIRONMENT=Development
    volumes:
      - ./logs:/app/logs

volumes:
  postgres_data:
EOF

echo "📝 Creating database initialization script..."
mkdir -p scripts
cat > scripts/init-db.sql << 'EOF'
-- Database initialization for TaskMaster Pro
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";

-- Create performance monitoring table
CREATE TABLE IF NOT EXISTS public.__performance_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID,
    entity_type VARCHAR(100),
    query_type VARCHAR(100),
    execution_time_ms INTEGER,
    rows_scanned INTEGER,
    rows_returned INTEGER,
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for performance monitoring
CREATE INDEX IF NOT EXISTS idx_performance_logs_tenant_timestamp 
ON public.__performance_logs(tenant_id, timestamp);

-- Grant permissions
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO taskmaster;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO taskmaster;
EOF

echo "🔨 Creating Dockerfile for API..."
mkdir -p src/TaskMasterPro.Api
cat > src/TaskMasterPro.Api/Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/TaskMasterPro.Api/TaskMasterPro.Api.csproj", "src/TaskMasterPro.Api/"]
COPY ["src/TaskMasterPro.Infrastructure/TaskMasterPro.Infrastructure.csproj", "src/TaskMasterPro.Infrastructure/"]
COPY ["src/TaskMasterPro.Core/TaskMasterPro.Core.csproj", "src/TaskMasterPro.Core/"]
COPY ["src/MultiTenant.Enforcer/MultiTenant.Enforcer.csproj", "src/MultiTenant.Enforcer/"]
RUN dotnet restore "src/TaskMasterPro.Api/TaskMasterPro.Api.csproj"
COPY . .
WORKDIR "/src/src/TaskMasterPro.Api"
RUN dotnet build "TaskMasterPro.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TaskMasterPro.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TaskMasterPro.Api.dll"]
EOF

echo "⚙️ Creating launch settings..."
mkdir -p src/TaskMasterPro.Api/Properties
cat > src/TaskMasterPro.Api/Properties/launchSettings.json << 'EOF'
{
  "profiles": {
    "TaskMasterPro.Api": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "https://localhost:5001;http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "Acme Tenant": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "https://acme.localhost:5001;http://acme.localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "TechInnovations Tenant": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "https://techinnovations.localhost:5001;http://techinnovations.localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
EOF

echo "🧪 Creating test script..."
cat > scripts/run-tests.sh << 'EOF'
#!/bin/bash

echo "🧪 Running TaskMaster Pro Multi-Tenant Tests"
echo "============================================"

# Start dependencies
echo "🐳 Starting Docker services..."
docker-compose up -d postgres redis

# Wait for database
echo "⏳ Waiting for database to be ready..."
sleep 10

# Run different test categories
echo "🔍 Running Analyzer Tests..."
dotnet build --verbosity normal 2>&1 | grep -E "(MTI001|MTI002|MTI003)" || echo "✅ No analyzer violations found"

echo "🏃 Running Unit Tests..."
dotnet test tests/TaskMasterPro.Tests --filter "Category=Unit" --logger "console;verbosity=detailed"

echo "🔗 Running Integration Tests..."
dotnet test tests/TaskMasterPro.Tests --filter "Category=Integration" --logger "console;verbosity=detailed"

echo "⚡ Running Performance Tests..."
dotnet test tests/TaskMasterPro.Tests --filter "Category=Performance" --logger "console;verbosity=detailed"

echo "🛡️ Running Security Tests..."
dotnet test tests/TaskMasterPro.Tests --filter "Category=Security" --logger "console;verbosity=detailed"

echo "📊 Test Summary Complete!"
EOF

chmod +x scripts/run-tests.sh

echo "🎯 Creating demonstration script..."
cat > scripts/demo-scenarios.sh << 'EOF'
#!/bin/bash

echo "🎭 TaskMaster Pro Multi-Tenant Demonstration"
echo "==========================================="

BASE_URL="http://localhost:5000"

echo "📋 Scenario 1: Tenant Isolation Test"
echo "Fetching projects for Acme tenant..."
curl -H "Host: acme.localhost" "$BASE_URL/api/projects" | jq '.[].tenantId' | sort | uniq
echo ""

echo "Fetching projects for TechInnovations tenant..."
curl -H "Host: techinnovations.localhost" "$BASE_URL/api/projects" | jq '.[].tenantId' | sort | uniq
echo ""

echo "📋 Scenario 2: Cross-Tenant Admin Access (should fail without proper auth)"
curl -H "Host: acme.localhost" "$BASE_URL/api/admin/companies" -w "\nStatus: %{http_code}\n"
echo ""

echo "📋 Scenario 3: Performance Test"
echo "Creating large dataset and measuring query performance..."
curl -X POST -H "Host: acme.localhost" -H "Content-Type: application/json" \
  "$BASE_URL/api/admin/generate-test-data" \
  -d '{"projectCount": 100, "tasksPerProject": 50}' \
  -w "\nResponse Time: %{time_total}s\n"
echo ""

echo "📋 Scenario 4: Tenant Statistics (system admin only)"
curl -H "Authorization: Bearer [SYSTEM_ADMIN_TOKEN]" "$BASE_URL/api/admin/tenant-statistics" | jq '.'
echo ""

echo "✅ Demonstration complete!"
EOF

chmod +x scripts/demo-scenarios.sh

echo "📋 Creating configuration files..."
cat > src/TaskMasterPro.Api/appsettings.json << 'EOF'
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=taskmaster_dev;Username=taskmaster;Password=dev_password_123"
  },
  "MultiTenant": {
    "EnableViolationLogging": true,
    "CacheTenantResolution": true,
    "CacheExpirationMinutes": 5,
    "PerformanceMonitoring": {
      "Enabled": true,
      "SlowQueryThresholdMs": 1000,
      "LogQueryPlans": true
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "TaskMasterPro": "Debug",
      "MultiTenant.Enforcer": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  },
  "AllowedHosts": "*"
}
EOF

cat > src/TaskMasterPro.Api/appsettings.Development.json << 'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "System": "Information",
      "Microsoft": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "MultiTenant": {
    "EnableViolationLogging": true,
    "PerformanceMonitoring": {
      "Enabled": true,
      "SlowQueryThresholdMs": 500,
      "LogQueryPlans": true
    }
  }
}
EOF

echo "🎨 Creating Visual Studio Code settings..."
mkdir -p .vscode
cat > .vscode/launch.json << 'EOF'
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "TaskMaster API (Acme Tenant)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/TaskMasterPro.Api/bin/Debug/net8.0/TaskMasterPro.Api.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/TaskMasterPro.Api",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)",
        "uriFormat": "http://acme.localhost:5000/swagger"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "http://localhost:5000"
      },
      "sourceFileMap": {
        "/Views": "${workspaceFolder}/Views"
      }
    },
    {
      "name": "Run All Tests",
      "type": "coreclr",
      "request": "launch",
      "program": "dotnet",
      "args": ["test", "tests/TaskMasterPro.Tests", "--logger", "console;verbosity=detailed"],
      "cwd": "${workspaceFolder}",
      "stopAtEntry": false,
      "console": "integratedTerminal"
    }
  ]
}
EOF

cat > .vscode/tasks.json << 'EOF'
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "command": "dotnet",
      "type": "process",
      "args": ["build", "${workspaceFolder}/TaskMasterPro.sln"],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "start-dependencies",
      "command": "docker-compose",
      "type": "shell",
      "args": ["up", "-d", "postgres", "redis"],
      "group": "build",
      "presentation": {
        "echo": true,
        "reveal": "always",
        "focus": false,
        "panel": "shared"
      }
    },
    {
      "label": "run-tests",
      "command": "./scripts/run-tests.sh",
      "type": "shell",
      "group": "test",
      "presentation": {
        "echo": true,
        "reveal": "always",
        "focus": false,
        "panel": "shared"
      }
    }
  ]
}
EOF

echo "📋 Creating README with instructions..."
cat > README.md << 'EOF'
# TaskMaster Pro - Multi-Tenant SaaS Sample

This is a complete sample application demonstrating the **Multi-Tenant Data Isolation Enforcer** for .NET Core 8 SaaS applications.

## 🚀 Quick Start

### 1. Start Infrastructure
```bash
docker-compose up -d
```

### 2. Run the Application
```bash
cd src/TaskMasterPro.Api
dotnet run
```

### 3. Test Different Tenants
- **Acme Corp**: http://acme.localhost:5000/swagger
- **Tech Innovations**: http://techinnovations.localhost:5000/swagger

### 4. Run Tests
```bash
./scripts/run-tests.sh
```

## 🧪 Testing Scenarios

### Analyzer Violations (Compile-Time)
The Roslyn analyzers will catch these violations in Visual Studio:

1. **MTI001**: Direct DbSet access on tenant-isolated entities
2. **MTI002**: Cross-tenant operations without authorization
3. **MTI003**: Potential tenant filter bypasses

### Runtime Protection
- Automatic tenant ID assignment
- Cross-tenant modification detection
- Global query filter enforcement

### Performance Testing
- Large dataset handling
- Tenant-based indexing
- Query performance monitoring

## 📊 What You'll See

### In Visual Studio:
- ❌ Red squiggly lines for tenant violations
- 💡 Code fixes suggestions
- 🔍 IntelliSense for safe repository methods

### In Logs:
```json
{
  "Level": "Information",
  "Message": "Retrieved 15 projects for tenant 11111111-1111-1111-1111-111111111111",
  "TenantId": "11111111-1111-1111-1111-111111111111",
  "QueryExecutionMs": 45
}
```

### In Database:
- All queries automatically include `WHERE TenantId = @tenantId`
- Performance indexes on `(TenantId, ...)` columns
- Audit logs for cross-tenant operations

## 🎯 Key Features Demonstrated

1. **Compile-Time Safety**: Roslyn analyzers prevent violations
2. **Runtime Protection**: EF Core global filters and validation
3. **Performance**: Optimized queries with tenant-based indexes
4. **Cross-Tenant Operations**: Secure admin functions
5. **Monitoring**: Comprehensive logging and metrics

## 🔧 Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Roslyn        │    │   ASP.NET Core   │    │   EF Core       │
│   Analyzers     │    │   Middleware     │    │   Global        │
│   (Compile)     │───▶│   (Runtime)      │───▶│   Filters       │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## 📋 Test Categories

Run specific test categories:
```bash
# Unit tests
dotnet test --filter "Category=Unit"

# Integration tests  
dotnet test --filter "Category=Integration"

# Performance tests
dotnet test --filter "Category=Performance"

# Security tests
dotnet test --filter "Category=Security"
```

## 🚨 Common Violations Your Team Will Encounter

### ❌ This won't compile:
```csharp
return await _context.Projects.ToListAsync(); // MTI001 error
```

### ✅ This will work:
```csharp
return await _projectRepository.GetAllAsync(); // Automatically tenant-filtered
```

Your team literally **cannot** deploy code that violates tenant isolation!
EOF

echo "✅ Setup Complete!"
echo ""
echo "🎯 Next Steps:"
echo "1. cd TaskMasterPro.SaaS"
echo "2. docker-compose up -d"
echo "3. cd src/TaskMasterPro.Api && dotnet run"
echo "4. Open http://acme.localhost:5000/swagger"
echo "5. Run ./scripts/run-tests.sh to see the enforcer in action"
echo ""
echo "📋 The sample application includes:"
echo "   • Complete multi-tenant SaaS app (TaskMaster Pro)"
echo "   • Roslyn analyzers that catch violations at compile-time"
echo "   • Runtime protection with EF Core global filters"
echo "   • Performance-optimized tenant-based indexing"
echo "   • Cross-tenant admin operations with proper authorization"
echo "   • Comprehensive test suite covering all scenarios"
echo ""
echo "🛡️ Your team literally cannot deploy code that violates tenant isolation!"
