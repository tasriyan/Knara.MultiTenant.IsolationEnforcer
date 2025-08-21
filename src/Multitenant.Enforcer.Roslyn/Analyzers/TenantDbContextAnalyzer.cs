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

	public static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
	{
		var classDeclaration = (ClassDeclarationSyntax)context.Node;
		var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

		if (classSymbol == null)
			return;

		// Check if class directly inherits from DbContext
		if (!EntityFrameworkChecks.IsDirectDbContextInheritance(classSymbol))
			return;

		// Check if class has DbSet properties with tenant-isolated entities
		var tenantIsolatedDbSets = TenantChecks.GetTenantIsolatedDbSetProperties(classSymbol);

		if (tenantIsolatedDbSets.Any())
		{
			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.DbContextMustInheritTenantDbContext,
				classDeclaration.Identifier.GetLocation(),
				classSymbol.Name);

			context.ReportDiagnostic(diagnostic);
		}
	}
}
