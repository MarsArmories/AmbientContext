using System.Globalization;
using System.Text;
using AmbientContext.Abstractions;
using AmbientContext.Core;
using AmbientContext.Generators;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbientContext.Generator.Tests;

[TestClass]
public sealed class AmbientContextGeneratorTests
{
    [TestMethod]
    public void Generates_public_contracts_and_internal_implementations()
    {
        var result = RunGenerator("""
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Guid), "ClientId", Namespace = "MyApp.Contexts")]
            """);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public interface IClientIdAccessor");
        result.GeneratedSource.Should().Contain("public interface IClientIdContext");
        result.GeneratedSource.Should().Contain("internal sealed class ClientIdAmbientContextMarker");
        result.GeneratedSource.Should().Contain("internal sealed class ClientIdAccessor");
        result.GeneratedSource.Should().Contain("internal sealed class ClientIdContext");
        result.GeneratedSource.Should().Contain("public static IServiceCollection AddClientIdContext");
        result.GeneratedSource.Should().NotContain("ValueTask");
    }

    [TestMethod]
    public void Generates_nullable_annotations_for_accessors()
    {
        var referenceResult = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(string), "TenantId", Namespace = "MyApp.Contexts")]
            """);
        var valueResult = RunGenerator("""
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Guid), "ClientId", Namespace = "MyApp.Contexts")]
            """);

        referenceResult.GeneratedSource.Should().Contain("string? CurrentOrDefault");
        referenceResult.GeneratedSource.Should().Contain(
            "bool TryGetCurrent([NotNullWhen(true)] out string? value)");
        valueResult.GeneratedSource.Should().Contain("global::System.Guid? CurrentOrDefault");
        valueResult.GeneratedSource.Should().Contain(
            "bool TryGetCurrent(out global::System.Guid value)");
    }

    [TestMethod]
    public void Does_not_generate_aggregate_registration_without_opt_in()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(string), "TenantId", Namespace = "MyApp.Contexts")]
            """);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().NotContain("IHostBuilder");
        result.GeneratedSource.Should().NotContain("AddMyAppAmbientContexts");
    }

    [TestMethod]
    public void Generates_configured_aggregate_registration()
    {
        var result = RunGenerator("""
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContextRegistration("AddMyAppAmbientContexts")]
            [assembly: AmbientContext(typeof(Guid), "ClientId", Namespace = "MyApp.Contexts")]
            [assembly: AmbientContext(typeof(string), "TenantId", Namespace = "MyApp.Contexts")]
            """);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(
            "public static IServiceCollection AddMyAppAmbientContexts(this IServiceCollection services)");
        result.GeneratedSource.Should().Contain(
            "public static IHostBuilder AddMyAppAmbientContexts(this IHostBuilder hostBuilder)");
        result.GeneratedSource.Should().Contain(
            "global::MyApp.Contexts.ClientIdAmbientContextServiceCollectionExtensions.AddClientIdContext(services);");
        result.GeneratedSource.Should().Contain(
            "global::MyApp.Contexts.TenantIdAmbientContextServiceCollectionExtensions.AddTenantIdContext(services);");
    }

    [TestMethod]
    public void Ordinary_async_lambda_compiles_without_a_delegate_cast()
    {
        var result = RunGenerator("""
            using System;
            using System.Threading.Tasks;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Guid), "ClientId", Namespace = "MyApp.Contexts")]

            namespace MyApp.Contexts;

            public sealed class Worker
            {
                public Task RunAsync(IClientIdContext context, Guid clientId)
                {
                    return context.ExecuteAsClientIdAsync(clientId, async cancellationToken =>
                    {
                        await Task.Yield();
                    });
                }
            }
            """);

        result.Diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("123ClientId", "MAACGEN001")]
    [DataRow("Client-Id", "MAACGEN001")]
    public void Rejects_invalid_context_name(string name, string diagnosticId)
    {
        var result = RunGenerator($$"""
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Guid), "{{name}}")]
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain(diagnosticId);
    }

    [TestMethod]
    public void Rejects_escaped_context_name_with_clear_diagnostic()
    {
        var result = RunGenerator("""
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Guid), "@ClientId")]
            """);

        var diagnostic = result.Diagnostics.Should()
            .ContainSingle(item => item.Id == "MAACGEN001")
            .Which;

        diagnostic.GetMessage(CultureInfo.InvariantCulture)
            .Should().Contain("non-escaped C# identifier");
    }

    [TestMethod]
    public void Rejects_duplicate_context_name()
    {
        var result = RunGenerator("""
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Guid), "ClientId")]
            [assembly: AmbientContext(typeof(string), "ClientId")]
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN002");
    }

    [TestMethod]
    public void Rejects_nullable_value_type()
    {
        var result = RunGenerator("""
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Guid?), "ClientId")]
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN003");
    }

    [TestMethod]
    public void Rejects_void_value_type()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(void), "Nothing")]
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN004");
    }

    [TestMethod]
    public void Rejects_static_value_type()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(StaticValues), "Values")]

            public static class StaticValues
            {
            }
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN004");
    }

    [TestMethod]
    public void Rejects_invalid_namespace()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(string), "TenantId", Namespace = "Not-A.Namespace")]
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN006");
    }

    [TestMethod]
    public void Rejects_open_generic_value_type()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Dictionary<,>), "Lookup")]
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN007");
    }

    [TestMethod]
    public void Rejects_generated_type_name_collision()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(string), "TenantId", Namespace = "MyApp.Contexts")]

            namespace MyApp.Contexts;
            public interface ITenantIdAccessor
            {
            }
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN008");
    }

    [TestMethod]
    public void Rejects_duplicate_registration_declarations()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContextRegistration("AddOne")]
            [assembly: AmbientContextRegistration("AddTwo")]
            [assembly: AmbientContext(typeof(string), "TenantId")]
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN009");
    }

    [TestMethod]
    public void Rejects_invalid_registration_method_name()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContextRegistration("Add-All")]
            [assembly: AmbientContext(typeof(string), "TenantId")]
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN010");
    }

    [TestMethod]
    public void Rejects_escaped_registration_method_name_with_clear_diagnostic()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContextRegistration("@AddAll")]
            [assembly: AmbientContext(typeof(string), "TenantId")]
            """);

        var diagnostic = result.Diagnostics.Should()
            .ContainSingle(item => item.Id == "MAACGEN010")
            .Which;

        diagnostic.GetMessage(CultureInfo.InvariantCulture)
            .Should().Contain("non-escaped C# identifier");
    }

    [TestMethod]
    public void Rejects_registration_name_that_matches_a_selective_method()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContextRegistration("AddTenantIdContext")]
            [assembly: AmbientContext(typeof(string), "TenantId")]
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN011");
    }

    [TestMethod]
    public void Distinct_aggregate_names_compile_across_multiple_libraries()
    {
        var libraryA = CompileLibrary(
            "LibraryA",
            """
            using AmbientContext.Abstractions;

            [assembly: AmbientContextRegistration("AddLibraryAAmbientContexts")]
            [assembly: AmbientContext(typeof(string), "TenantId", Namespace = "LibraryA")]
            """);
        var libraryB = CompileLibrary(
            "LibraryB",
            """
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContextRegistration("AddLibraryBAmbientContexts")]
            [assembly: AmbientContext(typeof(Guid), "ClientId", Namespace = "LibraryB")]
            """);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var appCompilation = CSharpCompilation.Create(
            "Application",
            new[]
            {
                CSharpSyntaxTree.ParseText(
                    """
                    using Microsoft.Extensions.DependencyInjection;

                    public static class Startup
                    {
                        public static void Configure(IServiceCollection services)
                        {
                            services.AddLibraryAAmbientContexts();
                            services.AddLibraryBAmbientContexts();
                        }
                    }
                    """,
                    parseOptions)
            },
            GetReferences().Concat(new[] { libraryA, libraryB }),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        appCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();
    }

    private static GeneratorResult RunGenerator(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var sourceTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CSharpCompilation.Create(
            "AmbientContext.GeneratorTest",
            new[] { sourceTree },
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(
            new[] { new AmbientContextGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        var outputDiagnostics = outputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var generatedSource = outputCompilation.SyntaxTrees
            .Where(tree => !ReferenceEquals(tree, sourceTree))
            .Select(tree => tree.ToString())
            .Aggregate(new StringBuilder(), static (builder, sourceText) =>
                builder.AppendLine(sourceText))
            .ToString();

        return new GeneratorResult(
            generatedSource,
            generatorDiagnostics.Concat(outputDiagnostics).ToArray());
    }

    private static PortableExecutableReference CompileLibrary(string assemblyName, string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(
            new[] { new AmbientContextGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);
        generatorDiagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();

        using var stream = new MemoryStream();
        var emitResult = outputCompilation.Emit(stream);
        emitResult.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        return trustedPlatformAssemblies.Concat(new[]
        {
            MetadataReference.CreateFromFile(typeof(AmbientContextAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(AmbientContextRuntime<,>).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Microsoft.Extensions.Hosting.IHostBuilder).Assembly.Location)
        });
    }

    private sealed record GeneratorResult(
        string GeneratedSource,
        IReadOnlyCollection<Diagnostic> Diagnostics);
}
