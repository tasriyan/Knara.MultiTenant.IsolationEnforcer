using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

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
			if (IsDbSetMethod(method) && firstTypeArg != null && IsTenantIsolatedEntity(firstTypeArg))
			{
				var entityTypeName = firstTypeArg.Name;
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.DirectDbSetAccess,
					memberAccess.GetLocation(),
					entityTypeName);

				context.ReportDiagnostic(diagnostic);
			}

			// Check for DbContext.Set<T>() method calls
			if (IsDbContextSetMethod(method) && firstTypeArg != null && IsTenantIsolatedEntity(firstTypeArg))
			{
				var entityTypeName = firstTypeArg.Name;
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.DirectDbSetAccess,
					memberAccess.GetLocation(),
					entityTypeName);

				context.ReportDiagnostic(diagnostic);
			}
		}

		// Check for DbSet property access
		if (memberSymbol is IPropertySymbol property && IsDbSetProperty(property))
		{
			if (property.Type is INamedTypeSymbol namedType &&
				namedType.TypeArguments.Length > 0 &&
				IsTenantIsolatedEntity(namedType.TypeArguments.First()))
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

	private static bool IsTenantIsolatedEntity(ITypeSymbol? type)
	{
		if (type == null) return false;

		return type.AllInterfaces.Any(i =>
			i.Name == "ITenantIsolated" &&
			i.ContainingNamespace.ToDisplayString().StartsWith("Multitenant.Enforcer"));
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

	private static bool IsDbSetMethod(IMethodSymbol method)
	{
		return method.ContainingType.Name == "DbSet" &&
			   method.ContainingType.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
	}

	private static bool IsDbContextSetMethod(IMethodSymbol method)
	{
		return method.Name == "Set" &&
			   method.ContainingType != null &&
			   IsDbContextType(method.ContainingType);
	}

	private static bool IsDbSetProperty(IPropertySymbol property)
	{
		return property.Type.Name == "DbSet" &&
			   property.Type.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
	}
}
