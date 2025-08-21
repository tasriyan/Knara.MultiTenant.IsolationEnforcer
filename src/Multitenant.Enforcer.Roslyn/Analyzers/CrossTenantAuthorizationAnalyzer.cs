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

	public static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
	{
		var methodDecl = (MethodDeclarationSyntax)context.Node;
		var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDecl);

		if (methodSymbol == null) return;

		// Check if method uses ICrossTenantOperationManager but lacks authorization attribute
		if (CrossTenantChecks.UsesCrossTenantManager(methodDecl, context.SemanticModel) &&
			!CrossTenantChecks.HasCrossTenantAttribute(methodSymbol))
		{
			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.MissingCrossTenantAttribute,
				methodDecl.Identifier.GetLocation());

			context.ReportDiagnostic(diagnostic);
		}

		// Check for unauthorized system context creation
		if (CrossTenantChecks.UsesSystemContextCreation(methodDecl, context.SemanticModel) &&
			!CrossTenantChecks.HasCrossTenantAttribute(methodSymbol))
		{
			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.UnauthorizedSystemContext,
				methodDecl.Identifier.GetLocation());

			context.ReportDiagnostic(diagnostic);
		}
	}

	public static void AnalyzeLambdaExpression(SyntaxNodeAnalysisContext context)
	{
		var lambda = context.Node;

		// Check if lambda uses ICrossTenantOperationManager
		if (CrossTenantChecks.UsesCrossTenantManagerInLambda(lambda, context.SemanticModel))
		{
			// Find the containing class to check for [AllowCrossTenantAccess] attribute
			var containingClass = lambda.FirstAncestorOrSelf<ClassDeclarationSyntax>();
			if (containingClass != null)
			{
				var classSymbol = context.SemanticModel.GetDeclaredSymbol(containingClass);
				if (classSymbol != null && !CrossTenantChecks.HasCrossTenantAttributeOnClass(classSymbol))
				{
					var diagnostic = Diagnostic.Create(
						DiagnosticDescriptors.MissingCrossTenantAttribute,
						lambda.GetLocation());

					context.ReportDiagnostic(diagnostic);
				}
			}
		}
	}

}