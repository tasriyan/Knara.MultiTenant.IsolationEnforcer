using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Knara.MultiTenant.IsolationEnforcer.Analyzers.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DirectDbAccessAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[DiagnosticDescriptors.DirectDbSetAccess];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
	}

	public static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
	{
		var memberAccess = (MemberAccessExpressionSyntax)context.Node;
		var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol;

		if (memberSymbol is IMethodSymbol method)
		{
			// Check for direct DbSet<T> access where T : ITenantIsolated
			var firstTypeArg = method.TypeArguments.FirstOrDefault();
			if (EntityFrameworkChecks.IsDbSetMethod(method) && firstTypeArg != null && TenantChecks.IsTenantIsolatedEntity(firstTypeArg))
			{
				// Check if this access is safe (through TenantIsolatedDbContext)
				if (!TenantChecks.IsSafeDbAccess(memberAccess, context.SemanticModel))
				{
					var entityTypeName = firstTypeArg.Name;
					var diagnostic = Diagnostic.Create(
						DiagnosticDescriptors.DirectDbSetAccess,
						memberAccess.GetLocation(),
						entityTypeName);

					context.ReportDiagnostic(diagnostic);
				}
			}

			// Check for DbContext.Set<T>() method calls
			if (EntityFrameworkChecks.IsDbContextSetMethod(method) && firstTypeArg != null && TenantChecks.IsTenantIsolatedEntity(firstTypeArg))
			{
				// Check if this access is safe (through TenantIsolatedDbContext)
				if (!TenantChecks.IsSafeDbAccess(memberAccess, context.SemanticModel))
				{
					var entityTypeName = firstTypeArg.Name;
					var diagnostic = Diagnostic.Create(
						DiagnosticDescriptors.DirectDbSetAccess,
						memberAccess.GetLocation(),
						entityTypeName);

					context.ReportDiagnostic(diagnostic);
				}
			}
		}

		// Check for DbSet property access
		if (memberSymbol is IPropertySymbol property && EntityFrameworkChecks.IsDbSetProperty(property))
		{
			if (property.Type is INamedTypeSymbol namedType &&
				namedType.TypeArguments.Length > 0 &&
				TenantChecks.IsTenantIsolatedEntity(namedType.TypeArguments.First()))
			{
				// Check if this access is safe (through TenantIsolatedDbContext)
				if (!TenantChecks.IsSafeDbAccess(memberAccess, context.SemanticModel))
				{
					var entityTypeName = namedType.TypeArguments.First().Name;
					var diagnostic = Diagnostic.Create(
						DiagnosticDescriptors.DirectDbSetAccess,
						memberAccess.GetLocation(),
						entityTypeName);

					context.ReportDiagnostic(diagnostic);
				}
			}
		}
	}
}
