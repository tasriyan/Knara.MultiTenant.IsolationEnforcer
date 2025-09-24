using Knara.MultiTenant.IsolationEnforcer.Analyzers.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace AnalyzersTests;

public class TenantChecksTests
{
	public class HasTenantIsolatedTypeArgument
	{
		[Fact]
		public void If_TypeArgument_IsTenantIsolated_ThenReturnsTrue()
		{
			// Arrange
			var mockProperty = new Mock<IPropertySymbol>();
			var mockNamedType = new Mock<INamedTypeSymbol>();
			var mockTypeArgument = new Mock<ITypeSymbol>();
			var mockInterface = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockInterface.Setup(x => x.Name).Returns("ITenantIsolated");
			mockInterface.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.Core");

			mockTypeArgument.Setup(x => x.AllInterfaces).Returns([mockInterface.Object]);
			mockNamedType.Setup(x => x.TypeArguments).Returns([mockTypeArgument.Object]);
			mockProperty.Setup(x => x.Type).Returns(mockNamedType.Object);

			// Act
			var result = TenantChecks.HasTenantIsolatedTypeArgument(mockProperty.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_TypeArguments_EmptyList_ThenReturnsFalse()
		{
			// Arrange
			var mockProperty = new Mock<IPropertySymbol>();
			var mockNamedType = new Mock<INamedTypeSymbol>();

			mockNamedType.Setup(x => x.TypeArguments).Returns([]);
			mockProperty.Setup(x => x.Type).Returns(mockNamedType.Object);

			// Act
			var result = TenantChecks.HasTenantIsolatedTypeArgument(mockProperty.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_TypeArgument_IsNonNamedType_ThenReturnsFalse()
		{
			// Arrange
			var mockProperty = new Mock<IPropertySymbol>();
			var mockType = new Mock<ITypeSymbol>();

			mockProperty.Setup(x => x.Type).Returns(mockType.Object);

			// Act
			var result = TenantChecks.HasTenantIsolatedTypeArgument(mockProperty.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_TypeArgument_IsNot_TenantIsolated_ThenReturnsFalse()
		{
			// Arrange
			var mockProperty = new Mock<IPropertySymbol>();
			var mockNamedType = new Mock<INamedTypeSymbol>();
			var mockTypeArgument = new Mock<ITypeSymbol>();
			var mockInterface = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockInterface.Setup(x => x.Name).Returns("IDisposable");
			mockInterface.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("System");

			mockTypeArgument.Setup(x => x.AllInterfaces).Returns([mockInterface.Object]);
			mockNamedType.Setup(x => x.TypeArguments).Returns([mockTypeArgument.Object]);
			mockProperty.Setup(x => x.Type).Returns(mockNamedType.Object);

			// Act
			var result = TenantChecks.HasTenantIsolatedTypeArgument(mockProperty.Object);

			// Assert
			result.ShouldBeFalse();
		}

	}

	public class IsTenantIsolatedEntity
	{
		[Fact]
		public void If_Entity_IsOfTenantIsolated_ThenReturnsTrue()
		{
			// Arrange
			var mockType = new Mock<ITypeSymbol>();
			var mockInterface = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockInterface.Setup(x => x.Name).Returns("ITenantIsolated");
			mockInterface.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.Core");

			mockType.Setup(x => x.AllInterfaces).Returns([mockInterface.Object]);

			// Act
			var result = TenantChecks.IsTenantIsolatedEntity(mockType.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_Entity_ComesFromMultiTenantEnforcerNamespace_ThenReturnsTrue()
		{
			// Arrange
			var mockType = new Mock<ITypeSymbol>();
			var mockInterface = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockInterface.Setup(x => x.Name).Returns("ITenantIsolated");
			mockInterface.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.Core");

			mockType.Setup(x => x.AllInterfaces).Returns([mockInterface.Object]);

			// Act
			var result = TenantChecks.IsTenantIsolatedEntity(mockType.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_EntityTypeIsNull_ThenReturnsFalse()
		{
			// Act
			var result = TenantChecks.IsTenantIsolatedEntity(null);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_Entity_ComesFromAnyOtherNamespace_ThenReturnsFalse()
		{
			// Arrange
			var mockType = new Mock<ITypeSymbol>();
			var mockInterface = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockInterface.Setup(x => x.Name).Returns("IDisposable");
			mockInterface.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("System");

			mockType.Setup(x => x.AllInterfaces).Returns([mockInterface.Object]);

			// Act
			var result = TenantChecks.IsTenantIsolatedEntity(mockType.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_Entity_IsOfTenantIsolated_ButComesFromAnyOtherNamespace_ThenReturnsFalse()
		{
			// Arrange
			var mockType = new Mock<ITypeSymbol>();
			var mockInterface = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockInterface.Setup(x => x.Name).Returns("ITenantIsolated");
			mockInterface.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("SomeOther.Namespace");

			mockType.Setup(x => x.AllInterfaces).Returns([mockInterface.Object]);

			// Act
			var result = TenantChecks.IsTenantIsolatedEntity(mockType.Object);

			// Assert
			result.ShouldBeFalse();
		}
	}

	public class IsTenantDbContextType
	{
		[Fact]
		public void If_DbContextType_IsTenantDbContext_ThenReturnsTrue()
		{
			// Arrange
			var mockType = new Mock<ITypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockType.Setup(x => x.Name).Returns("TenantIsolatedDbContext");
			mockType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.EntityFramework");

			// Act
			var result = TenantChecks.IsTenantDbContextType(mockType.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_DbContextType__IsInheritedFromTenantDbContext_ThenReturnsTrue()
		{
			// Arrange
			var mockDerivedType = new Mock<ITypeSymbol>();
			var mockBaseType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();
			var mockDerivedNamespace = new Mock<INamespaceSymbol>();

			mockDerivedType.Setup(x => x.Name).Returns("MyTenantDbContext");
			mockDerivedType.Setup(x => x.ContainingNamespace).Returns(mockDerivedNamespace.Object);
			mockDerivedType.Setup(x => x.BaseType).Returns(mockBaseType.Object);
			mockDerivedNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("MyApp.Data"); ;

			mockBaseType.Setup(x => x.Name).Returns("TenantIsolatedDbContext");
			mockBaseType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockBaseType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.EntityFramework");

			// Act
			var result = TenantChecks.IsTenantDbContextType(mockDerivedType.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_DbContextType_IsNotOfTenantDbContext_ThenReturnsFalse()
		{
			// Arrange
			var mockType = new Mock<ITypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockType.Setup(x => x.Name).Returns("DbContext");
			mockType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");

			// Act
			var result = TenantChecks.IsTenantDbContextType(mockType.Object);

			// Assert
			result.ShouldBeFalse();
		}
	}

	public class IsTenantEnforcerMethod
	{
		// Testing for common typo or refactoring mistakes: Multitenant vs MultiTenant
		[Fact]
		public void If_Method_ComesFrom_MultitenantEnforcer_Namespace_LowerCaseT_ThenReturnsTrue()
		{
			// Arrange
			var mockSymbol = new Mock<ISymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockSymbol.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.Core");

			// Act
			var result = TenantChecks.IsTenantEnforcerMethod(mockSymbol.Object);

			// Assert
			result.ShouldBeTrue();
		}

		// Testing for common typo or refactoring mistakes: Multitenant vs MultiTenant
		[Fact]
		public void If_Method_ComesFrom_MultiTenantEnforcer_Namespace_UpperCaseT_ThenReturnsTrue()
		{
			// Arrange
			var mockSymbol = new Mock<ISymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockSymbol.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.Core");

			// Act
			var result = TenantChecks.IsTenantEnforcerMethod(mockSymbol.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_Method_ComesFrom_MultitenantEnforcer_SubNamespace_ThenReturnsTrue()
		{
			// Arrange
			var mockSymbol = new Mock<ISymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockSymbol.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.EntityFramework");

			// Act
			var result = TenantChecks.IsTenantEnforcerMethod(mockSymbol.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_Method_ComesFrom_NonTenantEnforcer_Namespace_ThenReturnsFalse()
		{
			// Arrange
			var mockSymbol = new Mock<ISymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockSymbol.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("System.Collections.Generic");

			// Act
			var result = TenantChecks.IsTenantEnforcerMethod(mockSymbol.Object);

			// Assert
			result.ShouldBeFalse();
		}
	}

	public class GetTenantIsolatedDbSetProperties
	{
		[Fact]
		public void If_Properties_Are_TenantIsolatedDbSet_ThenReturnsFilteredProperties()
		{
			// Arrange
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			var mockTenantIsolatedProperty = new Mock<IPropertySymbol>();
			var mockNonTenantIsolatedProperty = new Mock<IPropertySymbol>();
			var mockNonDbSetProperty = new Mock<IPropertySymbol>();

			// Setup tenant isolated DbSet property
			SetupDbSetProperty(mockTenantIsolatedProperty, "DbSet", "Microsoft.EntityFrameworkCore", true);

			// Setup non-tenant isolated DbSet property  
			SetupDbSetProperty(mockNonTenantIsolatedProperty, "DbSet", "Microsoft.EntityFrameworkCore", false);

			// Setup non-DbSet property
			SetupNonDbSetProperty(mockNonDbSetProperty, "List", "System.Collections.Generic");

			ImmutableArray<ISymbol> members = 
				[	
					mockTenantIsolatedProperty.Object,
					mockNonTenantIsolatedProperty.Object,
					mockNonDbSetProperty.Object
				];

			mockClassSymbol.Setup(x => x.GetMembers()).Returns(members);

			// Act
			var result = TenantChecks.GetTenantIsolatedDbSetProperties(mockClassSymbol.Object);

			// Assert
			result.ShouldNotBeNull();
			result.Count().ShouldBe(1);
			result.First().ShouldBe(mockTenantIsolatedProperty.Object);
		}

		[Fact]
		public void If_Properties_Are_Not_DbSetProperties_ThenReturnsEmptyList()
		{
			// Arrange
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			var mockNonDbSetProperty = new Mock<IPropertySymbol>();

			SetupNonDbSetProperty(mockNonDbSetProperty, "List", "System.Collections.Generic");

			ImmutableArray<ISymbol> members = [mockNonDbSetProperty.Object ];
			mockClassSymbol.Setup(x => x.GetMembers()).Returns(members);

			// Act
			var result = TenantChecks.GetTenantIsolatedDbSetProperties(mockClassSymbol.Object);

			// Assert
			result.ShouldNotBeNull();
			result.Count().ShouldBe(0);
		}

		[Fact]
		public void If_Properties_AreOfDbSet_ButNOT_TenantIsolatedEntities_ThenReturnsEmptyList()
		{
			// Arrange
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			var mockNonTenantIsolatedProperty = new Mock<IPropertySymbol>();

			SetupDbSetProperty(mockNonTenantIsolatedProperty, "DbSet", "Microsoft.EntityFrameworkCore", false);

			ImmutableArray<ISymbol> members = [mockNonTenantIsolatedProperty.Object];
			mockClassSymbol.Setup(x => x.GetMembers()).Returns(members);

			// Act
			var result = TenantChecks.GetTenantIsolatedDbSetProperties(mockClassSymbol.Object);

			// Assert
			result.ShouldNotBeNull();
			result.Count().ShouldBe(0);
		}

		[Fact]
		public void If_DbContext_HasNoDbSetProperties_ThenReturnsEmptyList()
		{
			// Arrange
			var mockClassSymbol = new Mock<INamedTypeSymbol>();
			mockClassSymbol.Setup(x => x.GetMembers()).Returns([]);

			// Act
			var result = TenantChecks.GetTenantIsolatedDbSetProperties(mockClassSymbol.Object);

			// Assert
			result.ShouldNotBeNull();
			result.Count().ShouldBe(0);
		}
	}

	public class IsSafeDbAccess
	{
		[Fact]
		public void If_ExpressionType_IsTenantDbContext_ThenIsSafe()
		{
			// Arrange
			var mockExpressionType = new Mock<ITypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockExpressionType.Setup(x => x.Name).Returns("TenantIsolatedDbContext");
			mockExpressionType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockExpressionType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.EntityFramework");

			// Act
			var result = TenantChecks.IsSafeDbAccess(mockExpressionType.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_ExpressionType_IsDerivedFromTenantDbContext_ThenIsSafe()
		{
			// Arrange
			var mockDerivedType = new Mock<INamedTypeSymbol>();
			var mockBaseType = new Mock<INamedTypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();
			var mockDerivedNamespace = new Mock<INamespaceSymbol>();

			// Setup derived type that inherits from TenantIsolatedDbContext
			mockDerivedType.Setup(x => x.Name).Returns("MyTenantDbContext");
			mockDerivedType.Setup(x => x.ContainingNamespace).Returns(mockDerivedNamespace.Object);
			mockDerivedType.Setup(x => x.BaseType).Returns(mockBaseType.Object);
			mockDerivedNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("MyApp.Data");

			// Setup the base type as TenantIsolatedDbContext
			mockBaseType.Setup(x => x.Name).Returns("TenantIsolatedDbContext");
			mockBaseType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockBaseType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.EntityFramework");

			// Act
			var result = TenantChecks.IsSafeDbAccess(mockDerivedType.Object);

			// Assert
			result.ShouldBeTrue();
		}

		[Fact]
		public void If_ExpressionType_IsRegularDbContext_ThenIsNOTSafe()
		{
			// Arrange
			var mockExpressionType = new Mock<ITypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockExpressionType.Setup(x => x.Name).Returns("DbContext");
			mockExpressionType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockExpressionType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Microsoft.EntityFrameworkCore");

			// Act
			var result = TenantChecks.IsSafeDbAccess(mockExpressionType.Object);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_ExpressionType_IsNull_ThenConsiderItUnsafe()
		{
			// Act
			var result = TenantChecks.IsSafeDbAccess((ITypeSymbol?)null);

			// Assert
			result.ShouldBeFalse();
		}

		[Fact]
		public void If_ExpressionType_IsNotDbContext_ThenConsiderItUnsafe()
		{
			// Arrange
			var mockExpressionType = new Mock<ITypeSymbol>();
			var mockNamespace = new Mock<INamespaceSymbol>();

			mockExpressionType.Setup(x => x.Name).Returns("SomeService");
			mockExpressionType.Setup(x => x.ContainingNamespace).Returns(mockNamespace.Object);
			mockExpressionType.Setup(x => x.BaseType).Returns((INamedTypeSymbol)null);
			mockNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("MyApp.Services");

			// Act
			var result = TenantChecks.IsSafeDbAccess(mockExpressionType.Object);

			// Assert
			result.ShouldBeFalse();
		}
	}

	#region Helper Methods

	private static void SetupDbSetProperty(Mock<IPropertySymbol> mockProperty, string typeName, string namespaceName, bool isTenantIsolated)
	{
		var mockPropertyType = new Mock<INamedTypeSymbol>();
		var mockTypeNamespace = new Mock<INamespaceSymbol>();
		var mockTypeArgument = new Mock<ITypeSymbol>();

		// Setup DbSet type
		mockPropertyType.Setup(x => x.Name).Returns(typeName);
		mockPropertyType.Setup(x => x.ContainingNamespace).Returns(mockTypeNamespace.Object);
		mockTypeNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns(namespaceName);

		// Setup type argument
		if (isTenantIsolated)
		{
			var mockInterface = new Mock<INamedTypeSymbol>();
			var mockInterfaceNamespace = new Mock<INamespaceSymbol>();

			mockInterface.Setup(x => x.Name).Returns("ITenantIsolated");
			mockInterface.Setup(x => x.ContainingNamespace).Returns(mockInterfaceNamespace.Object);
			mockInterfaceNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("Knara.MultiTenant.IsolationEnforcer.Core");

			mockTypeArgument.Setup(x => x.AllInterfaces).Returns([mockInterface.Object]);
		}
		else
		{
			var mockInterface = new Mock<INamedTypeSymbol>();
			var mockInterfaceNamespace = new Mock<INamespaceSymbol>();

			mockInterface.Setup(x => x.Name).Returns("IDisposable");
			mockInterface.Setup(x => x.ContainingNamespace).Returns(mockInterfaceNamespace.Object);
			mockInterfaceNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns("System");

			mockTypeArgument.Setup(x => x.AllInterfaces).Returns([mockInterface.Object]);
		}

		mockPropertyType.Setup(x => x.TypeArguments).Returns([mockTypeArgument.Object]);
		mockProperty.Setup(x => x.Type).Returns(mockPropertyType.Object);
	}

	private static void SetupNonDbSetProperty(Mock<IPropertySymbol> mockProperty, string typeName, string namespaceName)
	{
		var mockPropertyType = new Mock<INamedTypeSymbol>();
		var mockTypeNamespace = new Mock<INamespaceSymbol>();

		mockPropertyType.Setup(x => x.Name).Returns(typeName);
		mockPropertyType.Setup(x => x.ContainingNamespace).Returns(mockTypeNamespace.Object);
		mockTypeNamespace.Setup(x => x.ToDisplayString(It.IsAny<SymbolDisplayFormat>())).Returns(namespaceName);

		mockProperty.Setup(x => x.Type).Returns(mockPropertyType.Object);
	}

	#endregion
}
