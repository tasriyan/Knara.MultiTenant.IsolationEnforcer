using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

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
			   baseType.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
	}

	private static IEnumerable<IPropertySymbol> GetTenantIsolatedDbSetProperties(INamedTypeSymbol classSymbol)
	{
		return classSymbol.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(IsDbSetProperty)
			.Where(property => HasTenantIsolatedTypeArgument(property));
	}

	private static bool IsDbSetProperty(IPropertySymbol property)
	{
		return property.Type.Name == "DbSet" &&
			   property.Type.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
	}

	private static bool HasTenantIsolatedTypeArgument(IPropertySymbol property)
	{
		if (property.Type is INamedTypeSymbol namedType &&
			namedType.TypeArguments.Length > 0)
		{
			var typeArgument = namedType.TypeArguments.First();
			return IsTenantIsolatedEntity(typeArgument);
		}

		return false;
	}

	private static bool IsTenantIsolatedEntity(ITypeSymbol? type)
	{
		if (type == null) return false;

		return type.AllInterfaces.Any(i =>
			i.Name == "ITenantIsolated" &&
			(i.ContainingNamespace.ToDisplayString().StartsWith("Multitenant.Enforcer") ||
			 i.ContainingNamespace.ToDisplayString().StartsWith("MultiTenant.Enforcer")));
	}
}
