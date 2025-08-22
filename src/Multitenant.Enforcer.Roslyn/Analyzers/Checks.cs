using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Multitenant.Enforcer.Roslyn;

public static class CommonChecks
{
	public static bool IsInsideLambdaExpression(SyntaxNode node)
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
}

public static class TenantChecks
{
	public static bool HasTenantIsolatedTypeArgument(IPropertySymbol property)
	{
		if (property.Type is INamedTypeSymbol namedType &&
			namedType.TypeArguments.Length > 0)
		{
			var typeArgument = namedType.TypeArguments.First();
			return IsTenantIsolatedEntity(typeArgument);
		}

		return false;
	}

	public static bool IsTenantIsolatedEntity(ITypeSymbol? type)
	{
		if (type == null) return false;

		// Only check for ITenantIsolated interface, NOT ICrossTenantAccessible
		return type.AllInterfaces.Any(i => i.Name == "ITenantIsolated" && IsTenantEnforcerMethod(i));
	}

	public static bool IsTenantDbContextType(ITypeSymbol type)
	{
		var current = type;
		while (current != null)
		{
			if (current.Name == "TenantIsolatedDbContext" && IsTenantEnforcerMethod(current))
			{
				return true;
			}
			current = current.BaseType;
		}
		return false;
	}

	public static bool IsTenantEnforcerMethod(ISymbol symbol)
	{
		return symbol.ContainingNamespace.ToDisplayString().StartsWith("Multitenant.Enforcer") ||
			   symbol.ContainingNamespace.ToDisplayString().StartsWith("MultiTenant.Enforcer");
	}

	public static bool IsSafeDbAccess(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
	{
		// Get the expression that we're accessing the member on (e.g., "context" in "context.ProjectTasks")
		var expression = memberAccess.Expression;
		var expressionTypeInfo = semanticModel.GetTypeInfo(expression);
		var expressionType = expressionTypeInfo.Type;

		return IsSafeDbAccess(expressionType);
	}

	public static bool IsSafeDbAccess(ITypeSymbol? expressionType)
	{
		if (expressionType != null)
		{
			// Check if the type is a safe DbContext (inherits from TenantDbContext)
			return IsTenantDbContextType(expressionType);
		}

		return false;
	}

	public static IEnumerable<IPropertySymbol> GetTenantIsolatedDbSetProperties(INamedTypeSymbol classSymbol)
	{
		return classSymbol.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(EntityFrameworkChecks.IsDbSetProperty)
			.Where(property => HasTenantIsolatedTypeArgument(property));
	}
}

public static class EntityFrameworkChecks
{
	public static bool IsDbSetProperty(IPropertySymbol property)
	{
		return property.Type.Name == "DbSet" &&
			IsEntityFrameworkMethod(property.Type);
	}

	public static bool IsDbContextType(ITypeSymbol type)
	{
		var current = type;
		while (current != null)
		{
			if (current.Name == "DbContext" && IsEntityFrameworkMethod(current))
			{
				return true;
			}
			current = current.BaseType;
		}
		return false;
	}

	public static bool IsDbSetMethod(IMethodSymbol method)
	{
		return method.ContainingType.Name == "DbSet" &&
			IsEntityFrameworkMethod(method.ContainingType);
	}

	public static bool IsDbContextSetMethod(IMethodSymbol method)
	{
		return method.Name == "Set" &&
			   method.ContainingType != null &&
			   IsDbContextType(method.ContainingType);
	}

	public static bool IsDirectDbContextInheritance(INamedTypeSymbol classSymbol)
	{
		var baseType = classSymbol.BaseType;
		if (baseType == null)
			return false;

		// Check if the immediate base class is DbContext
		return baseType.Name == "DbContext" &&
			IsEntityFrameworkMethod(baseType);
	}

	public static bool IsEntityFrameworkMethod(ISymbol symbol)
	{
		return symbol.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
	}
}

public static class CrossTenantChecks
{
	public static bool UsesSystemContextCreation(MethodDeclarationSyntax method, SemanticModel semanticModel)
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

	public static bool UsesCrossTenantManager(MethodDeclarationSyntax method, SemanticModel semanticModel)
	{
		// Look for ICrossTenantOperationManager usage in method body, excluding lambda expressions
		var descendantNodes = method.DescendantNodes();

		foreach (var node in descendantNodes)
		{
			// Skip nodes that are inside lambda expressions (they're handled separately)
			if (CommonChecks.IsInsideLambdaExpression(node))
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

	public static bool UsesCrossTenantManagerInLambda(SyntaxNode lambda, SemanticModel semanticModel)
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

	public static bool HasCrossTenantAttribute(IMethodSymbol method)
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

	public static bool IsCrossTenantManagerType(ITypeSymbol type)
	{
		if (type == null) return false;

		// Check the type name and namespace more thoroughly
		var fullName = type.ToDisplayString();
		return type.Name == "ICrossTenantOperationManager" ||
			   fullName.Contains("ICrossTenantOperationManager") ||
			   type.AllInterfaces.Any(i => i.Name == "ICrossTenantOperationManager" ||
										  i.ToDisplayString().Contains("ICrossTenantOperationManager"));
	}

	public static bool IsCrossTenantManagerMethod(IMethodSymbol methodSymbol)
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

	public static bool HasCrossTenantAttributeOnClass(ITypeSymbol typeSymbol)
	{
		return typeSymbol.GetAttributes().Any(attr =>
			attr.AttributeClass?.Name == "AllowCrossTenantAccessAttribute" ||
			attr.AttributeClass?.Name == "AllowCrossTenantAccess");
	}
}
