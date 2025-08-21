using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Multitenant.Enforcer.Roslyn;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TenantDbContextAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[DiagnosticDescriptors.DbContextMustInheritTenantDbContext];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
	}

	private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
	{
		var classDeclaration = (ClassDeclarationSyntax)context.Node;
		var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

		if (classSymbol == null)
			return;

		// Check if class directly inherits from DbContext
		if (!IsDirectDbContextInheritance(classSymbol))
			return;

		// Check if class has DbSet properties with tenant-isolated entities
		var tenantIsolatedDbSets = GetTenantIsolatedDbSetProperties(classSymbol);

		if (tenantIsolatedDbSets.Any())
		{
			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.DbContextMustInheritTenantDbContext,
				classDeclaration.Identifier.GetLocation(),
				classSymbol.Name);

			context.ReportDiagnostic(diagnostic);
		}
	}

	private static bool IsDirectDbContextInheritance(INamedTypeSymbol classSymbol)
	{
		var baseType = classSymbol.BaseType;
		if (baseType == null)
			return false;

		// Check if the immediate base class is DbContext
		return baseType.Name == "DbContext" &&
			CommonChecks.IsEntityFrameworkMethod(baseType);
			   //baseType.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
	}

	private static IEnumerable<IPropertySymbol> GetTenantIsolatedDbSetProperties(INamedTypeSymbol classSymbol)
	{
		return classSymbol.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(CommonChecks.IsDbSetProperty)
			.Where(property => CommonChecks.HasTenantIsolatedTypeArgument(property));
	}
}
