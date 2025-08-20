using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Multitenant.Enforcer.Roslyn;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RepositoryRequiredAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[DiagnosticDescriptors.TenantEntityWithoutRepository];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeGenericName, SyntaxKind.GenericName);
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
				if (typeSymbol != null && IsTenantIsolatedEntity(typeSymbol))
				{
					// This might be a direct entity access - check if it's in a repository context
					if (!IsInRepositoryContext(genericName))
					{
						var diagnostic = Diagnostic.Create(
							DiagnosticDescriptors.TenantEntityWithoutRepository,
							genericName.GetLocation(),
							typeSymbol?.Name ?? "Unknown");

						context.ReportDiagnostic(diagnostic);
					}
				}
			}
		}
	}

	private static bool IsTenantIsolatedEntity(ITypeSymbol type)
	{
		if (type == null) return false;

		return type.AllInterfaces.Any(i =>
			i.Name == "ITenantIsolated" &&
			i.ContainingNamespace.ToDisplayString().StartsWith("MultiTenant.Enforcer"));
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