using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MultiTenant.Enforcer.Core
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TenantIsolationCodeFixProvider))]
    [Shared]
    public class TenantIsolationCodeFixProvider : CodeFixProvider
    {
        private const string UseTenantRepositoryTitle = "Use ITenantRepository instead";
        private const string AddCrossTenantAttributeTitle = "Add [AllowCrossTenantAccess] attribute";
        private const string InjectTenantRepositoryTitle = "Inject ITenantRepository through constructor";

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
                    case "MTI001": // DirectDbSetAccess
                        await RegisterDbSetAccessFix(context, root, node, diagnostic);
                        break;

                    case "MTI002": // MissingCrossTenantAttribute
                        await RegisterCrossTenantAttributeFix(context, root, node, diagnostic);
                        break;

                    case "MTI004": // TenantEntityWithoutRepository
                        await RegisterRepositoryInjectionFix(context, root, node, diagnostic);
                        break;
                }
            }
        }

        private static async Task RegisterDbSetAccessFix(CodeFixContext context, SyntaxNode root, SyntaxNode node, Diagnostic diagnostic)
        {
            // Fix for direct DbSet access - replace with repository call
            if (node is MemberAccessExpressionSyntax memberAccess)
            {
                var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
                var entityType = ExtractEntityTypeFromDiagnostic(diagnostic);

                if (entityType != null)
                {
                    var action = CodeAction.Create(
                        title: UseTenantRepositoryTitle,
                        createChangedDocument: c => ReplaceDbSetWithRepository(context.Document, root, memberAccess, entityType, c),
                        equivalenceKey: UseTenantRepositoryTitle);

                    context.RegisterCodeFix(action, diagnostic);

                    // Also offer to inject the repository if not already available
                    var constructorAction = CodeAction.Create(
                        title: InjectTenantRepositoryTitle,
                        createChangedDocument: c => AddRepositoryInjection(context.Document, root, entityType, c),
                        equivalenceKey: InjectTenantRepositoryTitle);

                    context.RegisterCodeFix(constructorAction, diagnostic);
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

        private static async Task RegisterRepositoryInjectionFix(CodeFixContext context, SyntaxNode root, SyntaxNode node, Diagnostic diagnostic)
        {
            var entityType = ExtractEntityTypeFromDiagnostic(diagnostic);
            if (entityType != null)
            {
                var action = CodeAction.Create(
                    title: InjectTenantRepositoryTitle,
                    createChangedDocument: c => AddRepositoryInjection(context.Document, root, entityType, c),
                    equivalenceKey: InjectTenantRepositoryTitle);

                context.RegisterCodeFix(action, diagnostic);
            }
        }

        private static async Task<Document> ReplaceDbSetWithRepository(
            Document document,
            SyntaxNode root,
            MemberAccessExpressionSyntax memberAccess,
            string entityType,
            CancellationToken cancellationToken)
        {
            // Replace _context.Set<Entity>() or _context.Entities with _entityRepository
            var repositoryFieldName = $"_{char.ToLower(entityType[0])}{entityType.Substring(1)}Repository";

            // Create the replacement expression
            var replacement = SyntaxFactory.IdentifierName(repositoryFieldName);

            // If the original was a method call like Set<Entity>(), replace the entire invocation
            SyntaxNode newRoot;

			if (memberAccess.Expression is InvocationExpressionSyntax invocation)
            {
				newRoot = root.ReplaceNode(invocation, replacement);
			}
            else
				newRoot =root.ReplaceNode(memberAccess, replacement);

            // Add using statement if needed
            newRoot = AddUsingIfNeeded(newRoot, "MultiTenant.Enforcer.EntityFramework");

            return document.WithSyntaxRoot(newRoot);
        }

        private static async Task<Document> AddRepositoryInjection(
            Document document,
            SyntaxNode root,
            string entityType,
            CancellationToken cancellationToken)
        {
            var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDeclaration == null) return document;

            var repositoryFieldName = $"_{char.ToLower(entityType[0])}{entityType.Substring(1)}Repository";
            var repositoryTypeName = $"ITenantRepository<{entityType}>";

            // Add private readonly field
            var field = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName(repositoryTypeName))
                .WithVariables(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(
                            SyntaxFactory.Identifier(repositoryFieldName)))))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

            // Add constructor parameter
            var constructor = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
            if (constructor != null)
            {
                var parameter = SyntaxFactory.Parameter(
                    SyntaxFactory.Identifier(char.ToLower(entityType[0]) + entityType.Substring(1) + "Repository"))
                    .WithType(SyntaxFactory.IdentifierName(repositoryTypeName));

                var newParameterList = constructor.ParameterList.AddParameters(parameter);

                // Add assignment in constructor body
                var assignment = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(repositoryFieldName),
                        SyntaxFactory.IdentifierName(parameter.Identifier.ValueText)));

                var newBody = constructor.Body?.AddStatements(assignment) ?? 
                             SyntaxFactory.Block(assignment);

                var newConstructor = constructor
                    .WithParameterList(newParameterList)
                    .WithBody(newBody);

                var newClass = classDeclaration
                    .ReplaceNode(constructor, newConstructor)
                    .AddMembers(field);

                var newRoot = root.ReplaceNode(classDeclaration, newClass);
                newRoot = AddUsingIfNeeded(newRoot, "MultiTenant.Enforcer.EntityFramework");

                return document.WithSyntaxRoot(newRoot);
            }

            return document;
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
}

