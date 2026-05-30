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
    public void Generates_TenantIdContext_for_string()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(string), "TenantId", Namespace = "MyApp.Contexts")]
            """);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public sealed class TenantIdContext");
        result.GeneratedSource.Should().Contain("public interface ITenantIdAccessor");
        result.GeneratedSource.Should().Contain("public static IServiceCollection AddTenantIdContext");
    }

    [TestMethod]
    public void Generates_ClientIdContext_for_Guid()
    {
        var result = RunGenerator("""
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Guid), "ClientId", Namespace = "MyApp.Contexts")]
            """);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public sealed class ClientIdContext");
        result.GeneratedSource.Should().Contain("AmbientContextAccessor<ClientIdAmbientContextMarker, global::System.Guid>");
    }

    [TestMethod]
    public void Generates_nullable_annotations_for_reference_types()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(string), "TenantId", Namespace = "MyApp.Contexts")]
            """);

        result.GeneratedSource.Should().Contain("string? CurrentOrDefault");
        result.GeneratedSource.Should().Contain("bool TryGetCurrent([NotNullWhen(true)] out string? value)");
    }

    [TestMethod]
    public void Generates_ergonomic_TryGetCurrent_for_value_types()
    {
        var result = RunGenerator("""
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Guid), "ClientId", Namespace = "MyApp.Contexts")]
            """);

        result.GeneratedSource.Should().Contain("global::System.Guid? CurrentOrDefault");
        result.GeneratedSource.Should().Contain("bool TryGetCurrent(out global::System.Guid value)");
    }

    [TestMethod]
    public void Rejects_invalid_context_name()
    {
        var result = RunGenerator("""
            using System;
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(Guid), "123ClientId")]
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("MAACGEN001");
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
    public void Uses_explicit_namespace_from_attribute()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(string), "TenantId", Namespace = "MyApp.Contexts")]
            """);

        result.GeneratedSource.Should().Contain("namespace MyApp.Contexts;");
    }

    [TestMethod]
    public void Uses_default_generated_namespace_when_no_namespace_is_available()
    {
        var result = RunGenerator("""
            using AmbientContext.Abstractions;

            [assembly: AmbientContext(typeof(string), "TenantId")]
            """);

        result.GeneratedSource.Should().Contain("namespace AmbientContext.Generated;");
    }

    private static GeneratorResult RunGenerator(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "AmbientContext.GeneratorTest",
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

        var outputDiagnostics = outputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var allDiagnostics = generatorDiagnostics.Concat(outputDiagnostics).ToArray();
        var generatedSource = outputCompilation.SyntaxTrees
            .Skip(1)
            .Select(tree => tree.ToString())
            .SingleOrDefault() ?? string.Empty;

        return new GeneratorResult(generatedSource, allDiagnostics);
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        return trustedPlatformAssemblies.Concat(new[]
        {
            MetadataReference.CreateFromFile(typeof(AmbientContextAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(AmbientContextAccessor<,>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location)
        });
    }

    private sealed record GeneratorResult(string GeneratedSource, IReadOnlyCollection<Diagnostic> Diagnostics);
}
