using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace MultiTenant.Enforcer.Core
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TenantIsolationAnalyzer : DiagnosticAnalyzer
    {
        // Error: Direct DbSet access on tenant-isolated entities
        public static readonly DiagnosticDescriptor DirectDbSetAccess = new DiagnosticDescriptor(
            "MTI001",
            "Direct DbSet access on tenant-isolated entity",
            "Use ITenantRepository<{0}> instead of direct DbSet access to ensure tenant isolation",
            "Security",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Direct access to DbSet<T> bypasses tenant isolation. Use ITenantRepository<T> instead.",
            helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI001");

        // Error: Missing AllowCrossTenantAccess attribute for cross-tenant operations
        public static readonly DiagnosticDescriptor MissingCrossTenantAttribute = new DiagnosticDescriptor(
            "MTI002",
            "Cross-tenant operation without authorization",
            "Method using ICrossTenantOperationManager must have [AllowCrossTenantAccess] attribute",
            "Security",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Cross-tenant operations require explicit authorization with [AllowCrossTenantAccess] attribute.",
            helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI002");

        // Warning: Potential tenant filter bypass
        public static readonly DiagnosticDescriptor PotentialFilterBypass = new DiagnosticDescriptor(
            "MTI003",
            "Potential tenant filter bypass detected",
            "Query on {0} might bypass tenant filtering. Verify this is intentional and properly authorized.",
            "Security",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Query patterns that might bypass tenant filtering should be carefully reviewed.",
            helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI003");

        // Warning: Tenant isolated entity without proper repository
        public static readonly DiagnosticDescriptor TenantEntityWithoutRepository = new DiagnosticDescriptor(
            "MTI004",
            "Tenant-isolated entity accessed without repository",
            "Entity {0} implements ITenantIsolated but is not accessed through ITenantRepository<T>",
            "Security",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Tenant-isolated entities should be accessed through ITenantRepository<T> for proper isolation.",
            helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI004");

        // Error: System context usage without authorization
        public static readonly DiagnosticDescriptor UnauthorizedSystemContext = new DiagnosticDescriptor(
            "MTI005",
            "Unauthorized system context usage",
            "TenantContext.SystemContext() usage requires [AllowCrossTenantAccess] attribute",
            "Security",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Creating system context requires explicit authorization.",
            helpLinkUri: "https://docs.multitenant.enforcer/rules/MTI005");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                DirectDbSetAccess,
                MissingCrossTenantAttribute,
                PotentialFilterBypass,
                TenantEntityWithoutRepository,
                UnauthorizedSystemContext);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeGenericName, SyntaxKind.GenericName);
        }

        private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
        {
            var memberAccess = (MemberAccessExpressionSyntax)context.Node;
            var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol;

            if (memberSymbol is IMethodSymbol method)
            {
                // Check for direct DbSet<T> access where T : ITenantIsolated
                if (IsDbSetMethod(method) && IsTenantIsolatedEntity(method.TypeArguments.FirstOrDefault()))
                {
                    var entityTypeName = method.TypeArguments.First().Name;
                    var diagnostic = Diagnostic.Create(
                        DirectDbSetAccess,
                        memberAccess.GetLocation(),
                        entityTypeName);

                    context.ReportDiagnostic(diagnostic);
                }

                // Check for Set<T>() method on DbContext where T : ITenantIsolated
                if (IsDbContextSetMethod(method) && IsTenantIsolatedEntity(method.TypeArguments.FirstOrDefault()))
                {
                    var entityTypeName = method.TypeArguments.First().Name;
                    var diagnostic = Diagnostic.Create(
                        DirectDbSetAccess,
                        memberAccess.GetLocation(),
                        entityTypeName);

                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Check for DbSet property access
            if (memberSymbol is IPropertySymbol property && IsDbSetProperty(property))
            {
                if (IsTenantIsolatedEntity(property.Type))
                {
                    var entityTypeName = property.Type.Name;
                    var diagnostic = Diagnostic.Create(
                        DirectDbSetAccess,
                        memberAccess.GetLocation(),
                        entityTypeName);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDecl = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDecl);

            if (methodSymbol == null) return;

            // Check if method uses ICrossTenantOperationManager but lacks authorization attribute
            if (UsesCrossTenantManager(methodDecl, context.SemanticModel) &&
                !HasCrossTenantAttribute(methodSymbol))
            {
                var diagnostic = Diagnostic.Create(
                    MissingCrossTenantAttribute,
                    methodDecl.Identifier.GetLocation());

                context.ReportDiagnostic(diagnostic);
            }

            // Check for unauthorized system context creation
            if (UsesSystemContextCreation(methodDecl, context.SemanticModel) &&
                !HasCrossTenantAttribute(methodSymbol))
            {
                var diagnostic = Diagnostic.Create(
                    UnauthorizedSystemContext,
                    methodDecl.Identifier.GetLocation());

                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;

            if (memberAccess == null) return;

            var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
            if (symbolInfo.Symbol is IMethodSymbol method)
            {
                // Check for IgnoreQueryFilters() usage
                if (method.Name == "IgnoreQueryFilters" && IsEntityFrameworkMethod(method))
                {
                    // This might bypass tenant filtering
                    var diagnostic = Diagnostic.Create(
                        PotentialFilterBypass,
                        invocation.GetLocation(),
                        "IgnoreQueryFilters()");

                    context.ReportDiagnostic(diagnostic);
                }

                // Check for FromSqlRaw/FromSqlInterpolated usage
                if ((method.Name == "FromSqlRaw" || method.Name == "FromSqlInterpolated") &&
                    IsEntityFrameworkMethod(method))
                {
                    var diagnostic = Diagnostic.Create(
                        PotentialFilterBypass,
                        invocation.GetLocation(),
                        method.Name);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void AnalyzeGenericName(SyntaxNodeAnalysisContext context)
        {
            var genericName = (GenericNameSyntax)context.Node;

            // Check for generic type usage that might indicate direct entity access
            if (genericName.Identifier.ValueText == "DbSet" || genericName.Identifier.ValueText == "IQueryable")
            {
                var typeArgument = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                if (typeArgument != null)
                {
                    var typeSymbol = context.SemanticModel.GetTypeInfo(typeArgument).Type;
                    if (IsTenantIsolatedEntity(typeSymbol))
                    {
                        // This might be a direct entity access - check if it's in a repository context
                        if (!IsInRepositoryContext(genericName))
                        {
                            var diagnostic = Diagnostic.Create(
                                TenantEntityWithoutRepository,
                                genericName.GetLocation(),
                                typeSymbol?.Name ?? "Unknown");

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private static bool IsDbSetMethod(IMethodSymbol method)
        {
            return method.ContainingType.Name == "DbSet" &&
                   method.ContainingType.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
        }

        private static bool IsDbContextSetMethod(IMethodSymbol method)
        {
            return method.Name == "Set" &&
                   method.ContainingType.BaseType != null &&
                   IsDbContextType(method.ContainingType);
        }

        private static bool IsDbSetProperty(IPropertySymbol property)
        {
            return property.Type.Name == "DbSet" &&
                   property.Type.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
        }

        private static bool IsDbContextType(ITypeSymbol type)
        {
            var current = type;
            while (current != null)
            {
                if (current.Name == "DbContext" &&
                    current.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore"))
                {
                    return true;
                }
                current = current.BaseType;
            }
            return false;
        }

        private static bool IsTenantIsolatedEntity(ITypeSymbol? type)
        {
            if (type == null) return false;

            return type.AllInterfaces.Any(i =>
                i.Name == "ITenantIsolated" &&
                i.ContainingNamespace.ToDisplayString().StartsWith("MultiTenant.Enforcer"));
        }

        private static bool IsEntityFrameworkMethod(IMethodSymbol method)
        {
            return method.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
        }

        private static bool UsesCrossTenantManager(MethodDeclarationSyntax method, SemanticModel semanticModel)
        {
            // Look for ICrossTenantOperationManager usage in method body
            var descendantNodes = method.DescendantNodes();

            foreach (var node in descendantNodes)
            {
                if (node is MemberAccessExpressionSyntax memberAccess)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                    if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
                    {
                        if (methodSymbol.ContainingType.Name == "ICrossTenantOperationManager" ||
                            methodSymbol.Name == "ExecuteCrossTenantOperationAsync" ||
                            methodSymbol.Name == "BeginCrossTenantOperationAsync")
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool UsesSystemContextCreation(MethodDeclarationSyntax method, SemanticModel semanticModel)
        {
            var descendantNodes = method.DescendantNodes();

            foreach (var node in descendantNodes)
            {
                if (node is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.ValueText == "SystemContext")
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                    if (symbolInfo.Symbol is IMethodSymbol methodSymbol &&
                        methodSymbol.ContainingType.Name == "TenantContext")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasCrossTenantAttribute(IMethodSymbol method)
        {
            // Check method attributes
            if (method.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name == "AllowCrossTenantAccessAttribute" ||
                attr.AttributeClass?.Name == "AllowCrossTenantAccess"))
            {
                return true;
            }

            // Check class attributes
            if (method.ContainingType.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name == "AllowCrossTenantAccessAttribute" ||
                attr.AttributeClass?.Name == "AllowCrossTenantAccess"))
            {
                return true;
            }

            return false;
        }

        private static bool IsInRepositoryContext(SyntaxNode node)
        {
            // Walk up the syntax tree to see if we're in a repository class
            var current = node.Parent;
            while (current != null)
            {
                if (current is ClassDeclarationSyntax classDecl)
                {
                    if (classDecl.Identifier.ValueText.EndsWith("Repository") ||
                        classDecl.BaseList?.Types.Any(t => t.ToString().Contains("TenantRepository")) == true)
                    {
                        return true;
                    }
                }
                current = current.Parent;
            }

            return false;
        }
    }
}

