using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;

namespace Multitenant.Enforcer.Roslyn.Fixers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TenantIsolationCodeFixProvider))]
[Shared]
public class TenantIsolationCodeFixProvider : CodeFixProvider
{
	private const string AddCrossTenantAttributeTitle = "Add [AllowCrossTenantAccess] attribute";

	public sealed override ImmutableArray<string> FixableDiagnosticIds =>
		[
			"MTI001",
			"MTI002",
			"MTI004"
		];

	public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root == null) return;

		foreach (var diagnostic in context.Diagnostics)
		{
			var diagnosticSpan = diagnostic.Location.SourceSpan;
			var node = root.FindNode(diagnosticSpan);

			switch (diagnostic.Id)
			{
				case "MTI002": // MissingCrossTenantAttribute
					await RegisterCrossTenantAttributeFix(context, root, node, diagnostic);
					break;
			}
		}
	}

	private static async Task RegisterCrossTenantAttributeFix(CodeFixContext context, SyntaxNode root, SyntaxNode node, Diagnostic diagnostic)
	{
		// For minimal APIs, the attribute should be added to the class level
		// First, try to find a containing class
		var containingClass = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
		if (containingClass != null)
		{
			var action = CodeAction.Create(
				title: AddCrossTenantAttributeTitle,
				createChangedDocument: c => AddCrossTenantAccessAttributeToClass(context.Document, root, containingClass, c),
				equivalenceKey: AddCrossTenantAttributeTitle);

			context.RegisterCodeFix(action, diagnostic);
			return;
		}

		// Fallback to method level for traditional methods
		var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
		if (method != null)
		{
			var action = CodeAction.Create(
				title: AddCrossTenantAttributeTitle,
				createChangedDocument: c => AddCrossTenantAccessAttributeToMethod(context.Document, root, method, c),
				equivalenceKey: AddCrossTenantAttributeTitle);

			context.RegisterCodeFix(action, diagnostic);
		}
	}

	private static async Task<Document> AddCrossTenantAccessAttributeToClass(
		Document document,
		SyntaxNode root,
		ClassDeclarationSyntax classDeclaration,
		CancellationToken cancellationToken)
	{
		// Check if the class already has the attribute
		var hasAttribute = classDeclaration.AttributeLists
			.SelectMany(al => al.Attributes)
			.Any(attr => attr.Name.ToString().Contains("AllowCrossTenantAccess"));

		if (hasAttribute)
		{
			return document; // Already has the attribute
		}

		// Create the attribute with a default justification
		var attribute = SyntaxFactory.Attribute(
			SyntaxFactory.IdentifierName("AllowCrossTenantAccess"))
			.WithArgumentList(
				SyntaxFactory.AttributeArgumentList(
					SyntaxFactory.SeparatedList(
					[
						SyntaxFactory.AttributeArgument(
							SyntaxFactory.LiteralExpression(
								SyntaxKind.StringLiteralExpression,
								SyntaxFactory.Literal("TODO: Provide business justification for cross-tenant access"))),
						SyntaxFactory.AttributeArgument(
							SyntaxFactory.LiteralExpression(
								SyntaxKind.StringLiteralExpression,
								SyntaxFactory.Literal("SystemAdmin")))
					])));

		var attributeList = SyntaxFactory.AttributeList(
			SyntaxFactory.SingletonSeparatedList(attribute))
			.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

		var newClass = classDeclaration.AddAttributeLists(attributeList);
		var newRoot = root.ReplaceNode(classDeclaration, newClass);

		// Add using statement if needed
		newRoot = AddUsingIfNeeded(newRoot, "Multitenant.Enforcer.Core");

		return document.WithSyntaxRoot(newRoot);
	}

	private static async Task<Document> AddCrossTenantAccessAttributeToMethod(
		Document document,
		SyntaxNode root,
		MethodDeclarationSyntax method,
		CancellationToken cancellationToken)
	{
		// Check if the method already has the attribute
		var hasAttribute = method.AttributeLists
			.SelectMany(al => al.Attributes)
			.Any(attr => attr.Name.ToString().Contains("AllowCrossTenantAccess"));

		if (hasAttribute)
		{
			return document; // Already has the attribute
		}

		// Create the attribute with a default justification
		var attribute = SyntaxFactory.Attribute(
			SyntaxFactory.IdentifierName("AllowCrossTenantAccess"))
			.WithArgumentList(
				SyntaxFactory.AttributeArgumentList(
					SyntaxFactory.SeparatedList(
					[
						SyntaxFactory.AttributeArgument(
							SyntaxFactory.LiteralExpression(
								SyntaxKind.StringLiteralExpression,
								SyntaxFactory.Literal("TODO: Provide business justification for cross-tenant access"))),
						SyntaxFactory.AttributeArgument(
							SyntaxFactory.LiteralExpression(
								SyntaxKind.StringLiteralExpression,
								SyntaxFactory.Literal("SystemAdmin")))
					])));

		var attributeList = SyntaxFactory.AttributeList(
			SyntaxFactory.SingletonSeparatedList(attribute));

		var newMethod = method.AddAttributeLists(attributeList);
		var newRoot = root.ReplaceNode(method, newMethod);

		// Add using statement if needed
		newRoot = AddUsingIfNeeded(newRoot, "Multitenant.Enforcer.Core");

		return document.WithSyntaxRoot(newRoot);
	}

	private static SyntaxNode AddUsingIfNeeded(SyntaxNode root, string namespaceName)
	{
		if (root is CompilationUnitSyntax compilationUnit)
		{
			var hasUsing = compilationUnit.Usings.Any(u =>
				u.Name?.ToString() == namespaceName);

			if (!hasUsing)
			{
				var usingDirective = SyntaxFactory.UsingDirective(
					SyntaxFactory.IdentifierName(namespaceName));

				return compilationUnit.AddUsings(usingDirective);
			}
		}

		return root;
	}

	//private static string ExtractEntityTypeFromDiagnostic(Diagnostic diagnostic)
	//{
	//	// Extract entity type name from diagnostic message
	//	var message = diagnostic.GetMessage();

	//	// For MTI001: "Use ITenantRepository<EntityType> instead..."
	//	if (message.Contains("ITenantRepository<"))
	//	{
	//		var startIndex = message.IndexOf("ITenantRepository<") + "ITenantRepository<".Length;
	//		var endIndex = message.IndexOf(">", startIndex);
	//		if (endIndex > startIndex)
	//		{
	//			return message.Substring(startIndex, endIndex - startIndex);
	//		}
	//	}

	//	// For other diagnostics, try to extract from the message format
	//	if (diagnostic.Descriptor.MessageFormat.ToString().Contains("{0}"))
	//	{
	//		// The entity type is usually the first argument
	//		return diagnostic.Properties.TryGetValue("EntityType", out var entityType) ? entityType : null;
	//	}

	//	return null;
	//}
}

