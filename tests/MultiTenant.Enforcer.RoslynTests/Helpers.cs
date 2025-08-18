using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Multitenant.Enforcer.Roslyn;

namespace MultiTenant.Enforcer.RoslynTests;

public static class Helpers
{
	public static async Task VerifyCodeFixAsync(string testCode, DiagnosticResult expectedDiagnostic, string fixedCode)
	{
		var test = new CSharpCodeFixTest<TenantIsolationAnalyzer, TenantIsolationCodeFixProvider, DefaultVerifier>
		{
			TestCode = testCode,
			FixedCode = fixedCode,
			// Use .NET 8 reference assemblies (most stable for testing)
			ReferenceAssemblies = ReferenceAssemblies.Net.Net80
				.AddPackages([
					new PackageIdentity("Microsoft.EntityFrameworkCore", "8.0.0"),
					new PackageIdentity("Microsoft.AspNetCore.App.Ref", "8.0.0")
				])
		};

		test.ExpectedDiagnostics.Add(expectedDiagnostic);

		await test.RunAsync();
	}

	public static async Task VerifyCodeFixAsync(string testCode)
	{
		var test = new CSharpAnalyzerTest<TenantIsolationAnalyzer, DefaultVerifier>
		{
			TestCode = testCode
		};

		// Use .NET 8 reference assemblies (most stable for testing)
		test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80
			.AddPackages([
				new PackageIdentity("Microsoft.EntityFrameworkCore", "8.0.0"),
				new PackageIdentity("Microsoft.AspNetCore.App.Ref", "8.0.0")
			]);

		await test.RunAsync();
	}

	public static async Task VerifyCodeFixAsync(string testCode, DiagnosticResult[] expectedDiagnostics, string fixedCode)
	{
		var test = new CSharpCodeFixTest<TenantIsolationAnalyzer, TenantIsolationCodeFixProvider, DefaultVerifier>
		{
			TestCode = testCode,
			FixedCode = fixedCode
		};

		// Use .NET 8 reference assemblies (most stable for testing)
		test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80
			.AddPackages([
				new PackageIdentity("Microsoft.EntityFrameworkCore", "8.0.0"),
				new PackageIdentity("Microsoft.AspNetCore.App.Ref", "8.0.0")
			]);

		test.ExpectedDiagnostics.AddRange(expectedDiagnostics);

		await test.RunAsync();
	}
}
