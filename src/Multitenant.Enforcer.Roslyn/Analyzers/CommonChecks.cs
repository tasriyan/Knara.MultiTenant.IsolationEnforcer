using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Multitenant.Enforcer.Roslyn;

public static class CommonChecks
{
	public static bool IsDbSetProperty(IPropertySymbol property)
	{
		return property.Type.Name == "DbSet" &&
			IsEntityFrameworkMethod(property.Type);
			   //property.Type.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
	}

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
			if (current.Name == "TenantDbContext" && IsTenantEnforcerMethod(current))
			{
				return true;
			}
			current = current.BaseType;
		}
		return false;
	}

	public static bool IsDbContextType(ITypeSymbol type)
	{
		var current = type;
		while (current != null)
		{
			if (current.Name == "DbContext" && IsEntityFrameworkMethod(current))
				//current.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore"))
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
			   //method.ContainingType.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
	}

	public static bool IsDbContextSetMethod(IMethodSymbol method)
	{
		return method.Name == "Set" &&
			   method.ContainingType != null &&
			   IsDbContextType(method.ContainingType);
	}

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

	//public static bool IsEntityFrameworkMethod(IMethodSymbol method)
	//{
	//	return method.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
	//}
	public static bool IsEntityFrameworkMethod(ISymbol symbol)
	{
		return symbol.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore");
	}

	public static bool IsTenantEnforcerMethod(ISymbol symbol)
	{
		return symbol.ContainingNamespace.ToDisplayString().StartsWith("Multitenant.Enforcer") ||
			   symbol.ContainingNamespace.ToDisplayString().StartsWith("MultiTenant.Enforcer");
	}
}
