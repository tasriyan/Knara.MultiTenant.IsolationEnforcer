using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;

namespace Multitenant.Enforcer.Core;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TenantIsolationCodeFixProvider))]
[Shared]
public class TenantIsolationCodeFixProvider : CodeFixProvider
{
	private const string AddCrossTenantAttributeTitle = "Add [AllowCrossTenantAccess] attribute";

	public sealed override ImmutableArray<string> FixableDiagnosticIds =>
		ImmutableArray.Create(
			"MTI001", // DirectDbSetAccess
			"MTI002", // MissingCrossTenantAttribute
			"MTI004"  // TenantEntityWithoutRepository
		);

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
		// Fix for missing [AllowCrossTenantAccess] attribute
		var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
		if (method != null)
		{
			var action = CodeAction.Create(
				title: AddCrossTenantAttributeTitle,
				createChangedDocument: c => AddCrossTenantAccessAttribute(context.Document, root, method, c),
				equivalenceKey: AddCrossTenantAttributeTitle);

			context.RegisterCodeFix(action, diagnostic);
		}
	}

	private static async Task<Document> AddCrossTenantAccessAttribute(
		Document document,
		SyntaxNode root,
		MethodDeclarationSyntax method,
		CancellationToken cancellationToken)
	{
		// Create the attribute with a default justification
		var attribute = SyntaxFactory.Attribute(
			SyntaxFactory.IdentifierName("AllowCrossTenantAccess"))
			.WithArgumentList(
				SyntaxFactory.AttributeArgumentList(
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.AttributeArgument(
							SyntaxFactory.LiteralExpression(
								SyntaxKind.StringLiteralExpression,
								SyntaxFactory.Literal("TODO: Provide business justification for cross-tenant access"))))));

		var attributeList = SyntaxFactory.AttributeList(
			SyntaxFactory.SingletonSeparatedList(attribute));

		var newMethod = method.AddAttributeLists(attributeList);
		var newRoot = root.ReplaceNode(method, newMethod);

		// Add using statement if needed
		newRoot = AddUsingIfNeeded(newRoot, "MultiTenant.Enforcer.Core");

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

	private static string? ExtractEntityTypeFromDiagnostic(Diagnostic diagnostic)
	{
		// Extract entity type name from diagnostic message
		var message = diagnostic.GetMessage();

		// For MTI001: "Use ITenantRepository<EntityType> instead..."
		if (message.Contains("ITenantRepository<"))
		{
			var startIndex = message.IndexOf("ITenantRepository<") + "ITenantRepository<".Length;
			var endIndex = message.IndexOf(">", startIndex);
			if (endIndex > startIndex)
			{
				return message.Substring(startIndex, endIndex - startIndex);
			}
		}

		// For other diagnostics, try to extract from the message format
		if (diagnostic.Descriptor.MessageFormat.ToString().Contains("{0}"))
		{
			// The entity type is usually the first argument
			return diagnostic.Properties.TryGetValue("EntityType", out var entityType) ? entityType : null;
		}

		return null;
	}
}

