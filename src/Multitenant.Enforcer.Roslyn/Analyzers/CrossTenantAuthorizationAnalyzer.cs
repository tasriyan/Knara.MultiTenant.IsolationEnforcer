using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Multitenant.Enforcer.Roslyn;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CrossTenantAuthorizationAnalyzer : DiagnosticAnalyzer
{
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[DiagnosticDescriptors.MissingCrossTenantAttribute, 
		DiagnosticDescriptors.UnauthorizedSystemContext];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
		// Register lambda expression analysis for minimal APIs
		context.RegisterSyntaxNodeAction(AnalyzeLambdaExpression, SyntaxKind.SimpleLambdaExpression);
		context.RegisterSyntaxNodeAction(AnalyzeLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression);
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

	private static bool UsesCrossTenantManager(MethodDeclarationSyntax method, SemanticModel semanticModel)
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

	private static bool HasCrossTenantAttributeOnClass(ITypeSymbol typeSymbol)
	{
		return typeSymbol.GetAttributes().Any(attr =>
			attr.AttributeClass?.Name == "AllowCrossTenantAccessAttribute" ||
			attr.AttributeClass?.Name == "AllowCrossTenantAccess");
	}

}