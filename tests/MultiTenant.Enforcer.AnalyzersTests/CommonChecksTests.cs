using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Multitenant.Enforcer.Roslyn;

namespace MultiTenant.Enforcer.RoslynTests;

public class CommonChecksTests
{
	public class IsInsideLambdaExpression
	{
		[Fact]
		public void If_SimpleLambdaExpression_ThenReturnsTrue()
		{
			// Arrange
			var code = "x => x.Property";
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = tree.GetRoot();
			var identifierNode = root.DescendantNodes().OfType<IdentifierNameSyntax>().First();

			// Act
			var result = CommonChecks.IsInsideLambdaExpression(identifierNode);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_ParenthesizedLambdaExpression_ReturnsTrue()
		{
			// Arrange
			var code = "(x, y) => x.Property + y.Property";
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = tree.GetRoot();
			var identifierNode = root.DescendantNodes().OfType<IdentifierNameSyntax>().First();

			// Act
			var result = CommonChecks.IsInsideLambdaExpression(identifierNode);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_NoLambdaExpression_ReturnsFalse()
		{
			// Arrange
			var code = "var property = obj.Property;";
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = tree.GetRoot();
			var identifierNode = root.DescendantNodes().OfType<IdentifierNameSyntax>().First();

			// Act
			var result = CommonChecks.IsInsideLambdaExpression(identifierNode);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_NestedLambdaExpression_ReturnsTrue()
		{
			// Arrange
			var code = "list.Where(x => x.Items.Any(item => item.Property == value))";
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = tree.GetRoot();
			var propertyNode = root.DescendantNodes().OfType<IdentifierNameSyntax>()
				.First(n => n.Identifier.ValueText == "Property");

			// Act
			var result = CommonChecks.IsInsideLambdaExpression(propertyNode);

			// Assert
			result.ShouldBeTrue();
		}
	}

}
