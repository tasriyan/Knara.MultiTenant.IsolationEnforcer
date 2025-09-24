using Knara.MultiTenant.IsolationEnforcer.Analyzers.Analyzers;
using Microsoft.CodeAnalysis;

namespace MultiTenant.Enforcer.RoslynTests;

public class EntityFrameworkChecksTests
{
	public class IsDbSetProperty
	{
		[Fact]
		public void If_Property_Is_A_DbSetPropertyFromEntityFramework_ThenReturnsTrue()
		{
			// Arrange
			var mockProperty = new Mock<IPropertySymbol>();
			var mockType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockType.Setup(x => x.Name).Returns("DbSet");
			mockType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");
			mockProperty.Setup(x => x.Type).Returns(mockType.Object);

			// Act
			var result = EntityFrameworkChecks.IsDbSetProperty(mockProperty.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_Property_IsNotA_DbSetProperty_ReturnsFalse()
		{
			// Arrange
			var mockProperty = new Mock<IPropertySymbol>();
			var mockType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockType.Setup(x => x.Name).Returns("List");
			mockType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("System.Collections.Generic");
			mockProperty.Setup(x => x.Type).Returns(mockType.Object);

			// Act
			var result = EntityFrameworkChecks.IsDbSetProperty(mockProperty.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_Property_Is_A_DbSetProperty_ButFromEntityFramework_ReturnsFalse()
		{
			// Arrange
			var mockProperty = new Mock<IPropertySymbol>();
			var mockType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockType.Setup(x => x.Name).Returns("DbSet");
			mockType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("SomeOther.Namespace");
			mockProperty.Setup(x => x.Type).Returns(mockType.Object);

			// Act
			var result = EntityFrameworkChecks.IsDbSetProperty(mockProperty.Object);

			// Assert
			result.ShouldBeFalse();
		}
	}

	public class IsDbContextType
	{
		[Fact]
		public void If_TypeIs_OfDbContext_ThenReturnsTrue()
		{
			// Arrange
			var mockType = new Mock<ITypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockType.Setup(x => x.Name).Returns("DbContext");
			mockType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");

			// Act
			var result = EntityFrameworkChecks.IsDbContextType(mockType.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_TypeIsInherited_FromDbContext_ReturnsTrue()
		{
			// Arrange
			var mockDerivedType = new Mock<ITypeSymbol>();
			var mockBaseType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();
			var mockDerivedNamespace = new Mock<INamespaceSymbol>();

			mockDerivedType.Setup(x => x.Name).Returns("MyDbContext");
			mockDerivedType.Setup(x => x.ContainingNamespace).Returns(mockDerivedNamespace.Object);
			mockDerivedType.Setup(x => x.BaseType).Returns(mockBaseType.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("MyApp.Data");

			mockBaseType.Setup(x => x.Name).Returns("DbContext");
			mockBaseType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockBaseType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");

			// Act
			var result = EntityFrameworkChecks.IsDbContextType(mockDerivedType.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void Otherwise_ReturnsFalse()
		{
			// Arrange
			var mockType = new Mock<ITypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockType.Setup(x => x.Name).Returns("MyService");
			mockType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("MyApp.Services");

			// Act
			var result = EntityFrameworkChecks.IsDbContextType(mockType.Object);

			// Assert
			result.ShouldBeFalse();
		}
	}

	public class IsDbSetMethod
	{
		[Fact]
		public void If_Method_IsDbSet_And_IsEntityFrameworkMethod_ThenReturnsTrue()
		{
			// Arrange
			var mockMethod = new Mock<IMethodSymbol>();
			var mockContainingType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockContainingType.Setup(x => x.Name).Returns("DbSet");
			mockContainingType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");
			mockMethod.Setup(x => x.ContainingType).Returns(mockContainingType.Object);

			// Act
			var result = EntityFrameworkChecks.IsDbSetMethod(mockMethod.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void Otherwise_ReturnsFalse()
		{
			// Arrange
			var mockMethod = new Mock<IMethodSymbol>();
			var mockContainingType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockContainingType.Setup(x => x.Name).Returns("List");
			mockContainingType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("System.Collections.Generic");
			mockMethod.Setup(x => x.ContainingType).Returns(mockContainingType.Object);

			// Act
			var result = EntityFrameworkChecks.IsDbSetMethod(mockMethod.Object);

			// Assert
			result.ShouldBeFalse();
		}
	}

	public class IsDbContextSetMethod
	{
		[Fact]
		public void If_Method_IsSet_And_IsEntityFrameworkMethod_ThenReturnsTrue()
		{
			// Arrange
			var mockMethod = new Mock<IMethodSymbol>();
			var mockContainingType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockMethod.Setup(x => x.Name).Returns("Set");
			mockContainingType.Setup(x => x.Name).Returns("DbContext");
			mockContainingType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockContainingType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");
			mockMethod.Setup(x => x.ContainingType).Returns(mockContainingType.Object);

			// Act
			var result = EntityFrameworkChecks.IsDbContextSetMethod(mockMethod.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void Otherwise_ReturnsFalse()
		{
			// Arrange
			var mockMethod = new Mock<IMethodSymbol>();
			var mockContainingType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockMethod.Setup(x => x.Name).Returns("SaveChanges");
			mockContainingType.Setup(x => x.Name).Returns("DbContext");
			mockContainingType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockContainingType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");
			mockMethod.Setup(x => x.ContainingType).Returns(mockContainingType.Object);

			// Act
			var result = EntityFrameworkChecks.IsDbContextSetMethod(mockMethod.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void Otherswise_NullErrorChecking_ShouldReturnFalse()
		{
			// Arrange
			var mockMethod = new Mock<IMethodSymbol>();

			mockMethod.Setup(x => x.Name).Returns("Set");
			mockMethod.Setup(x => x.ContainingType).Returns((INamedTypeSymbol)null);

			// Act
			var result = EntityFrameworkChecks.IsDbContextSetMethod(mockMethod.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_Method_IsSet_ButNOT_IsEntityFrameworkMethod_ThenReturnsFalse()
		{
			// Arrange
			var mockMethod = new Mock<IMethodSymbol>();
			var mockContainingType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockMethod.Setup(x => x.Name).Returns("Set");
			mockContainingType.Setup(x => x.Name).Returns("MyService");
			mockContainingType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockContainingType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("MyApp.Services");
			mockMethod.Setup(x => x.ContainingType).Returns(mockContainingType.Object);

			// Act
			var result = EntityFrameworkChecks.IsDbContextSetMethod(mockMethod.Object);

			// Assert
			result.ShouldBeFalse();
		}
	}

	public class IsEntityFrameworkMethod
	{
		[Fact]
		public void If_ObjectOrMethodNamespace_Is_Microsoft_EntityFrameworkCore_ThenReturnsTrue()
		{
			// Arrange
			var mockSymbol = new Mock<ISymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockSymbol.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");

			// Act
			var result = EntityFrameworkChecks.IsEntityFrameworkMethod(mockSymbol.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_ObjectOrMethodNamespace_HasRoot_Microsoft_EntityFrameworkCore_ThenReturnsTrue()
		{
			// Arrange
			var mockSymbol = new Mock<ISymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockSymbol.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore.Metadata");

			// Act
			var result = EntityFrameworkChecks.IsEntityFrameworkMethod(mockSymbol.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_AnyOtherNamespace_ReturnsFalse()
		{
			// Arrange
			var mockSymbol = new Mock<ISymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockSymbol.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("System.Collections.Generic");

			// Act
			var result = EntityFrameworkChecks.IsEntityFrameworkMethod(mockSymbol.Object);

			// Assert
			result.ShouldBeFalse();
		}
	}

	public class IsDirectDbContextInheritance
	{
		[Fact]
		public void If_ClassSymbol_DirectlyInheritsFromDbContext_ThenReturnsTrue()
		{
			// Arrange
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			var mockBaseType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockClassSymbol.Setup(x => x.BaseType).Returns(mockBaseType.Object);
			mockBaseType.Setup(x => x.Name).Returns("DbContext");
			mockBaseType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");

			// Act
			var result = EntityFrameworkChecks.IsDirectDbContextInheritance(mockClassSymbol.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_ClassSymbol_DirectlyInheritsFromDbContext_InSubNamespace_ThenReturnsTrue()
		{
			// Arrange
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			var mockBaseType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockClassSymbol.Setup(x => x.BaseType).Returns(mockBaseType.Object);
			mockBaseType.Setup(x => x.Name).Returns("DbContext");
			mockBaseType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore.SqlServer");

			// Act
			var result = EntityFrameworkChecks.IsDirectDbContextInheritance(mockClassSymbol.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_ClassSymbol_HasNoBaseType_ThenReturnsFalse()
		{
			// Arrange
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			mockClassSymbol.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);

			// Act
			var result = EntityFrameworkChecks.IsDirectDbContextInheritance(mockClassSymbol.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_ClassSymbol_InheritsFromNonDbContextClass_ThenReturnsFalse()
		{
			// Arrange
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			var mockBaseType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockClassSymbol.Setup(x => x.BaseType).Returns(mockBaseType.Object);
			mockBaseType.Setup(x => x.Name).Returns("BaseController");
			mockBaseType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.AspNetCore.Mvc");

			// Act
			var result = EntityFrameworkChecks.IsDirectDbContextInheritance(mockClassSymbol.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_ClassSymbol_InheritsFromDbContextNamedClass_ButNotFromEntityFramework_ThenReturnsFalse()
		{
			// Arrange
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			var mockBaseType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockClassSymbol.Setup(x => x.BaseType).Returns(mockBaseType.Object);
			mockBaseType.Setup(x => x.Name).Returns("DbContext");
			mockBaseType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("MyCustom.Framework");

			// Act
			var result = EntityFrameworkChecks.IsDirectDbContextInheritance(mockClassSymbol.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_ClassSymbol_InheritsFromTenantDbContext_ThenReturnsFalse()
		{
			// Arrange
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			var mockBaseType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockClassSymbol.Setup(x => x.BaseType).Returns(mockBaseType.Object);
			mockBaseType.Setup(x => x.Name).Returns("TenantDbContext");
			mockBaseType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Multitenant.Enforcer.EntityFramework");

			// Act
			var result = EntityFrameworkChecks.IsDirectDbContextInheritance(mockClassSymbol.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_ClassSymbol_InheritsFromCustomDbContext_ThenReturnsFalse()
		{
			// Arrange  
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			var mockBaseType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockClassSymbol.Setup(x => x.BaseType).Returns(mockBaseType.Object);
			mockBaseType.Setup(x => x.Name).Returns("MyCustomDbContext");
			mockBaseType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");

			// Act
			var result = EntityFrameworkChecks.IsDirectDbContextInheritance(mockClassSymbol.Object);

			// Assert
			result.ShouldBeFalse();
		}
	}
}
