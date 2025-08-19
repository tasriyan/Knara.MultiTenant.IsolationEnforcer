using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Multitenant.Enforcer.Roslyn;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TenantIsolationAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		ImmutableArray.Create(
			DiagnosticDescriptors.DirectDbSetAccess,
			DiagnosticDescriptors.MissingCrossTenantAttribute,
			DiagnosticDescriptors.PotentialFilterBypass,
			DiagnosticDescriptors.TenantEntityWithoutRepository,
			DiagnosticDescriptors.UnauthorizedSystemContext);

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
		context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
		context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
		context.RegisterSyntaxNodeAction(AnalyzeGenericName, SyntaxKind.GenericName);
		// Register lambda expression analysis for minimal APIs
		context.RegisterSyntaxNodeAction(AnalyzeLambdaExpression, SyntaxKind.SimpleLambdaExpression);
		context.RegisterSyntaxNodeAction(AnalyzeLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression);
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
					DiagnosticDescriptors.DirectDbSetAccess,
					memberAccess.GetLocation(),
					entityTypeName);

				context.ReportDiagnostic(diagnostic);
			}

			// Check for Set<T>() method on DbContext where T : ITenantIsolated
			if (IsDbContextSetMethod(method) && IsTenantIsolatedEntity(method.TypeArguments.FirstOrDefault()))
			{
				var entityTypeName = method.TypeArguments.First().Name;
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
				DiagnosticDescriptors.MissingCrossTenantAttribute,
				methodDecl.Identifier.GetLocation());

			context.ReportDiagnostic(diagnostic);
		}

		// Check for unauthorized system context creation
		if (UsesSystemContextCreation(methodDecl, context.SemanticModel) &&
			!HasCrossTenantAttribute(methodSymbol))
		{
			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.UnauthorizedSystemContext,
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
					DiagnosticDescriptors.PotentialFilterBypass,
					invocation.GetLocation(),
					"IgnoreQueryFilters()");

				context.ReportDiagnostic(diagnostic);
			}

			// Check for FromSqlRaw/FromSqlInterpolated usage
			if ((method.Name == "FromSqlRaw" || method.Name == "FromSqlInterpolated") &&
				IsEntityFrameworkMethod(method))
			{
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.PotentialFilterBypass,
					invocation.GetLocation(),
					method.Name);

				context.ReportDiagnostic(diagnostic);
			}
		}
	}

	private static void AnalyzeLambdaExpression(SyntaxNodeAnalysisContext context)
	{
		var lambda = context.Node;

		// Check if lambda uses ICrossTenantOperationManager
		if (UsesCrossTenantManagerInLambda(lambda, context.SemanticModel))
		{
			// Find the containing class to check for [AllowCrossTenantAccess] attribute
			var containingClass = lambda.FirstAncestorOrSelf<ClassDeclarationSyntax>();
			if (containingClass != null)
			{
				var classSymbol = context.SemanticModel.GetDeclaredSymbol(containingClass);
				if (classSymbol != null && !HasCrossTenantAttributeOnClass(classSymbol))
				{
					var diagnostic = Diagnostic.Create(
						DiagnosticDescriptors.MissingCrossTenantAttribute,
						lambda.GetLocation());

					context.ReportDiagnostic(diagnostic);
				}
			}
		}
	}

	private static bool UsesCrossTenantManagerInLambda(SyntaxNode lambda, SemanticModel semanticModel)
	{
		var descendantNodes = lambda.DescendantNodes();

		foreach (var node in descendantNodes)
		{
			// Check for method calls that might be cross-tenant operations
			if (node is MemberAccessExpressionSyntax memberAccess)
			{
				var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
				if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
				{
					// More thorough checking for cross-tenant methods
					if (methodSymbol.Name == "ExecuteCrossTenantOperationAsync" ||
					    methodSymbol.Name == "BeginCrossTenantOperationAsync" ||
					    IsCrossTenantManagerType(methodSymbol.ContainingType))
					{
						return true;
					}
				}
			}

			// Check for parameter usage of ICrossTenantOperationManager type
			if (node is IdentifierNameSyntax identifier)
			{
				var symbolInfo = semanticModel.GetSymbolInfo(identifier);
				if (symbolInfo.Symbol is IParameterSymbol paramSymbol)
				{
					// More thorough type checking
					var typeName = paramSymbol.Type.Name;
					var fullTypeName = paramSymbol.Type.ToDisplayString();
					
					if (typeName == "ICrossTenantOperationManager" ||
					    fullTypeName.Contains("ICrossTenantOperationManager") ||
					    IsCrossTenantManagerType(paramSymbol.Type))
					{
						return true;
					}
				}
			}
		}
		return false;
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
							DiagnosticDescriptors.TenantEntityWithoutRepository,
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

	private static bool IsTenantIsolatedEntity(ITypeSymbol type)
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
		// Look for ICrossTenantOperationManager usage in method body, excluding lambda expressions
		var descendantNodes = method.DescendantNodes();

		foreach (var node in descendantNodes)
		{
			// Skip nodes that are inside lambda expressions (they're handled separately)
			if (IsInsideLambdaExpression(node))
				continue;

			if (node is MemberAccessExpressionSyntax memberAccess)
			{
				var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
				if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
				{
					if (IsCrossTenantManagerMethod(methodSymbol))
					{
						return true;
					}
				}
			}
		}

		return false;
	}

	private static bool IsInsideLambdaExpression(SyntaxNode node)
	{
		var current = node.Parent;
		while (current != null)
		{
			if (current.IsKind(SyntaxKind.SimpleLambdaExpression) || 
				current.IsKind(SyntaxKind.ParenthesizedLambdaExpression))
			{
				return true;
			}
			current = current.Parent;
		}
		return false;
	}

	private static bool IsCrossTenantManagerType(ITypeSymbol type)
	{
		if (type == null) return false;

		// Check the type name and namespace more thoroughly
		var fullName = type.ToDisplayString();
		return type.Name == "ICrossTenantOperationManager" ||
			   fullName.Contains("ICrossTenantOperationManager") ||
			   type.AllInterfaces.Any(i => i.Name == "ICrossTenantOperationManager" || 
			                              i.ToDisplayString().Contains("ICrossTenantOperationManager"));
	}

	private static bool IsCrossTenantManagerMethod(IMethodSymbol methodSymbol)
	{
		// Check if the method is on ICrossTenantOperationManager type
		if (IsCrossTenantManagerType(methodSymbol.ContainingType))
		{
			return true;
		}

		// Check by method name for known cross-tenant methods
		if (methodSymbol.Name == "ExecuteCrossTenantOperationAsync" ||
		    methodSymbol.Name == "BeginCrossTenantOperationAsync")
		{
			return true;
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
		return HasCrossTenantAttributeOnClass(method.ContainingType);
	}

	private static bool HasCrossTenantAttributeOnClass(ITypeSymbol typeSymbol)
	{
		return typeSymbol.GetAttributes().Any(attr =>
			attr.AttributeClass?.Name == "AllowCrossTenantAccessAttribute" ||
			attr.AttributeClass?.Name == "AllowCrossTenantAccess");
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