using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Multitenant.Enforcer.Roslyn;

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

	private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
	{
		var memberAccess = (MemberAccessExpressionSyntax)context.Node;
		var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol;

		if (memberSymbol is IMethodSymbol method)
		{
			// Check for direct DbSet<T> access where T : ITenantIsolated
			var firstTypeArg = method.TypeArguments.FirstOrDefault();
			if (CommonChecks.IsDbSetMethod(method) && firstTypeArg != null && CommonChecks.IsTenantIsolatedEntity(firstTypeArg))
			{
				// Check if this access is safe (through TenantDbContext)
				if (!IsSafeDbAccess(memberAccess, context.SemanticModel))
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
			if (CommonChecks.IsDbContextSetMethod(method) && firstTypeArg != null && CommonChecks.IsTenantIsolatedEntity(firstTypeArg))
			{
				// Check if this access is safe (through TenantDbContext)
				if (!IsSafeDbAccess(memberAccess, context.SemanticModel))
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
		if (memberSymbol is IPropertySymbol property && CommonChecks.IsDbSetProperty(property))
		{
			if (property.Type is INamedTypeSymbol namedType &&
				namedType.TypeArguments.Length > 0 &&
				CommonChecks.IsTenantIsolatedEntity(namedType.TypeArguments.First()))
			{
				// Check if this access is safe (through TenantDbContext)
				if (!IsSafeDbAccess(memberAccess, context.SemanticModel))
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

	private static bool IsSafeDbAccess(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
	{
		// Get the expression that we're accessing the member on (e.g., "context" in "context.ProjectTasks")
		var expression = memberAccess.Expression;
		var expressionTypeInfo = semanticModel.GetTypeInfo(expression);
		var expressionType = expressionTypeInfo.Type;

		if (expressionType != null)
		{
			// Check if the type is a safe DbContext (inherits from TenantDbContext)
			return CommonChecks.IsTenantDbContextType(expressionType);
		}

		return false;
	}
}
