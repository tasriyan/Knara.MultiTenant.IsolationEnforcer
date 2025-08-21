using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Multitenant.Enforcer.Roslyn;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class QueryFilterAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[DiagnosticDescriptors.PotentialFilterBypass];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
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
			if (method.Name == "IgnoreQueryFilters" && CommonChecks.IsEntityFrameworkMethod(method))
			{
				// This might bypass tenant filtering
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.PotentialFilterBypass,
					invocation.GetLocation(),
					"IgnoreQueryFilters()");

				context.ReportDiagnostic(diagnostic);
			}

			// Check for FromSqlRaw/FromSqlInterpolated usage
			if ((method.Name == "FromSqlRaw" || method.Name == "FromSqlInterpolated") &&
				CommonChecks.IsEntityFrameworkMethod(method))
			{
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.PotentialFilterBypass,
					invocation.GetLocation(),
					method.Name);

				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}
