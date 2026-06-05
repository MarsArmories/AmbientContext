using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AmbientContext.Generators;

/// <summary>
/// Generates named ambient context APIs from assembly-level declarations.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AmbientContextGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "AmbientContext.Abstractions.AmbientContextAttribute";
    private const string RegistrationAttributeMetadataName =
        "AmbientContext.Abstractions.AmbientContextRegistrationAttribute";
    private static readonly string GeneratorVersion =
        typeof(AmbientContextGenerator).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generationOutput = context.CompilationProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (source, cancellationToken) =>
                Collect(source.Left, source.Right, cancellationToken));

        context.RegisterSourceOutput(generationOutput, static (sourceProductionContext, output) =>
        {
            foreach (var item in output.Items)
            {
                if (item.Diagnostic is not null)
                {
                    sourceProductionContext.ReportDiagnostic(item.Diagnostic);
                    continue;
                }

                sourceProductionContext.AddSource(
                    $"AmbientContext_{item.Context!.Name}.g.cs",
                    SourceText.From(GenerateContext(item.Context), Encoding.UTF8));
            }

            var contexts = output.Items
                .Where(static item => item.Context is not null)
                .Select(static item => item.Context!)
                .OrderBy(static model => model.Namespace, StringComparer.Ordinal)
                .ThenBy(static model => model.Name, StringComparer.Ordinal)
                .ToImmutableArray();

            if (!contexts.IsDefaultOrEmpty && output.Registration is not null)
            {
                sourceProductionContext.AddSource(
                    "AmbientContext_Registration.g.cs",
                    SourceText.From(
                        GenerateRegistrationExtensions(contexts, output.Registration),
                        Encoding.UTF8));
            }
        });
    }

    private static GenerationOutput Collect(
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        var attributeSymbol = compilation.GetTypeByMetadataName(AttributeMetadataName);

        if (attributeSymbol is null)
        {
            return GenerationOutput.Empty;
        }

        var registrationAttributeSymbol =
            compilation.GetTypeByMetadataName(RegistrationAttributeMetadataName);
        var rootNamespace = GetRootNamespace(optionsProvider);
        var declarations = new List<AmbientContextDeclaration>();

        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
            {
                continue;
            }

            declarations.Add(CreateDeclaration(attribute, rootNamespace, cancellationToken));
        }

        var items = ImmutableArray.CreateBuilder<GenerationItem>();

        foreach (var declaration in declarations.Where(static declaration => declaration.Diagnostic is not null))
        {
            items.Add(GenerationItem.FromDiagnostic(declaration.Diagnostic!));
        }

        var validDeclarations = declarations
            .Where(static declaration => declaration.Context is not null)
            .ToArray();
        var duplicateNames = new HashSet<string>(
            validDeclarations
                .GroupBy(static declaration => declaration.Context!.Name)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key),
            StringComparer.Ordinal);

        foreach (var declaration in validDeclarations)
        {
            var model = declaration.Context!;

            if (duplicateNames.Contains(model.Name))
            {
                items.Add(GenerationItem.FromDiagnostic(Diagnostic.Create(
                    AmbientContextDiagnosticDescriptors.DuplicateName,
                    declaration.Location,
                    model.Name)));
                continue;
            }

            var collision = GetGeneratedTypeNames(model)
                .FirstOrDefault(typeName =>
                    compilation.GetTypeByMetadataName($"{model.Namespace}.{typeName}") is not null);

            if (collision is not null)
            {
                items.Add(GenerationItem.FromDiagnostic(Diagnostic.Create(
                    AmbientContextDiagnosticDescriptors.GeneratedNameCollision,
                    declaration.Location,
                    collision)));
                continue;
            }

            items.Add(GenerationItem.FromContext(model));
        }

        var contexts = items
            .Where(static item => item.Context is not null)
            .Select(static item => item.Context!)
            .ToImmutableArray();
        var registration = CollectRegistration(
            compilation,
            registrationAttributeSymbol,
            contexts,
            items,
            cancellationToken);

        return new GenerationOutput(items.ToImmutable(), registration);
    }

    private static AmbientContextDeclaration CreateDeclaration(
        AttributeData attribute,
        string rootNamespace,
        CancellationToken cancellationToken)
    {
        var location = GetLocation(attribute, cancellationToken);
        var valueType = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as ITypeSymbol
            : null;
        var name = attribute.ConstructorArguments.Length > 1
            ? attribute.ConstructorArguments[1].Value as string
            : null;
        var declaredNamespace = GetNamedArgument(attribute, "Namespace") as string;
        var targetNamespace = string.IsNullOrWhiteSpace(declaredNamespace)
            ? rootNamespace
            : declaredNamespace!;

        if (!IsComposableIdentifier(name))
        {
            return AmbientContextDeclaration.Invalid(Diagnostic.Create(
                AmbientContextDiagnosticDescriptors.InvalidName,
                location,
                name ?? string.Empty));
        }

        if (!IsValidNamespace(targetNamespace))
        {
            return AmbientContextDeclaration.Invalid(Diagnostic.Create(
                AmbientContextDiagnosticDescriptors.InvalidNamespace,
                location,
                targetNamespace,
                name));
        }

        if (valueType is null || valueType.TypeKind == TypeKind.Error)
        {
            return AmbientContextDeclaration.Invalid(Diagnostic.Create(
                AmbientContextDiagnosticDescriptors.UnresolvedValueType,
                location,
                name));
        }

        if (IsNullableValueType(valueType))
        {
            return AmbientContextDeclaration.Invalid(Diagnostic.Create(
                AmbientContextDiagnosticDescriptors.NullableValueType,
                location,
                name,
                valueType.ToDisplayString()));
        }

        if (ContainsTypeParameter(valueType))
        {
            return AmbientContextDeclaration.Invalid(Diagnostic.Create(
                AmbientContextDiagnosticDescriptors.OpenGenericValueType,
                location,
                name,
                valueType.ToDisplayString()));
        }

        if (IsUnsupported(valueType))
        {
            return AmbientContextDeclaration.Invalid(Diagnostic.Create(
                AmbientContextDiagnosticDescriptors.UnsupportedValueType,
                location,
                name,
                valueType.ToDisplayString()));
        }

        return AmbientContextDeclaration.Valid(
            name!,
            targetNamespace,
            valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            valueType.IsValueType,
            location);
    }

    private static AmbientContextRegistrationModel? CollectRegistration(
        Compilation compilation,
        INamedTypeSymbol? registrationAttributeSymbol,
        ImmutableArray<AmbientContextModel> contexts,
        ImmutableArray<GenerationItem>.Builder items,
        CancellationToken cancellationToken)
    {
        if (registrationAttributeSymbol is null)
        {
            return null;
        }

        var attributes = compilation.Assembly.GetAttributes()
            .Where(attribute =>
                SymbolEqualityComparer.Default.Equals(
                    attribute.AttributeClass,
                    registrationAttributeSymbol))
            .ToArray();

        if (attributes.Length == 0)
        {
            return null;
        }

        if (attributes.Length > 1)
        {
            foreach (var attribute in attributes)
            {
                items.Add(GenerationItem.FromDiagnostic(Diagnostic.Create(
                    AmbientContextDiagnosticDescriptors.DuplicateRegistration,
                    GetLocation(attribute, cancellationToken))));
            }

            return null;
        }

        var registrationAttribute = attributes[0];
        var location = GetLocation(registrationAttribute, cancellationToken);
        var methodName = registrationAttribute.ConstructorArguments.Length > 0
            ? registrationAttribute.ConstructorArguments[0].Value as string
            : null;

        if (!IsComposableIdentifier(methodName))
        {
            items.Add(GenerationItem.FromDiagnostic(Diagnostic.Create(
                AmbientContextDiagnosticDescriptors.InvalidRegistrationName,
                location,
                methodName ?? string.Empty)));
            return null;
        }

        if (contexts.Any(context =>
            string.Equals($"Add{context.Name}Context", methodName, StringComparison.Ordinal)))
        {
            items.Add(GenerationItem.FromDiagnostic(Diagnostic.Create(
                AmbientContextDiagnosticDescriptors.RegistrationNameCollision,
                location,
                methodName)));
            return null;
        }

        var model = new AmbientContextRegistrationModel(methodName!);
        var serviceTypeName =
            $"Microsoft.Extensions.DependencyInjection.{model.ServiceCollectionExtensionsName}";
        var hostTypeName = $"Microsoft.Extensions.Hosting.{model.HostBuilderExtensionsName}";

        if (compilation.GetTypeByMetadataName(serviceTypeName) is not null ||
            compilation.GetTypeByMetadataName(hostTypeName) is not null)
        {
            items.Add(GenerationItem.FromDiagnostic(Diagnostic.Create(
                AmbientContextDiagnosticDescriptors.GeneratedNameCollision,
                location,
                compilation.GetTypeByMetadataName(serviceTypeName) is not null
                    ? serviceTypeName
                    : hostTypeName)));
            return null;
        }

        return model;
    }

    private static string GenerateContext(AmbientContextModel model)
    {
        var markerName = $"{model.Name}AmbientContextMarker";
        var accessorInterfaceName = $"I{model.Name}Accessor";
        var accessorName = $"{model.Name}Accessor";
        var contextInterfaceName = $"I{model.Name}Context";
        var contextName = $"{model.Name}Context";
        var serviceCollectionExtensionsName = $"{model.Name}AmbientContextServiceCollectionExtensions";
        var executeMethodName = $"ExecuteAs{model.Name}";
        var executeAsyncMethodName = $"{executeMethodName}Async";
        var tryGetAttribute = model.IsValueType ? string.Empty : "[NotNullWhen(true)] ";
        var tryGetOutType = model.IsValueType ? model.ValueTypeName : $"{model.ValueTypeName}?";
        var valueCurrentOrDefaultType = $"{model.ValueTypeName}?";
        var valueType = model.ValueTypeName;

        return $$"""
            // <auto-generated />
            #nullable enable
            #pragma warning disable CS1591

            using System;
            using System.Diagnostics.CodeAnalysis;
            using System.Threading;
            using System.Threading.Tasks;
            using AmbientContext.Core;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection.Extensions;

            namespace {{model.Namespace}};

            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "{{GeneratorVersion}}")]
            [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            internal sealed class {{markerName}}
            {
            }

            /// <summary>
            /// Reads the current {{model.Name}} ambient value.
            /// </summary>
            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "{{GeneratorVersion}}")]
            public interface {{accessorInterfaceName}}
            {
                bool HasCurrent { get; }

                {{valueType}} Current { get; }

                {{valueCurrentOrDefaultType}} CurrentOrDefault { get; }

                bool TryGetCurrent({{tryGetAttribute}}out {{tryGetOutType}} value);
            }

            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "{{GeneratorVersion}}")]
            [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            internal sealed class {{accessorName}} : {{accessorInterfaceName}}
            {
                private readonly AmbientContextRuntime<{{markerName}}, {{valueType}}> _runtime;

                internal {{accessorName}}(AmbientContextRuntime<{{markerName}}, {{valueType}}> runtime)
                {
                    _runtime = runtime;
                }

                public bool HasCurrent => _runtime.HasCurrent;

                public {{valueType}} Current => _runtime.Current;

                public {{valueCurrentOrDefaultType}} CurrentOrDefault => _runtime.CurrentOrDefault;

            {{GenerateTryGetCurrent(model, tryGetAttribute, tryGetOutType)}}
            }

            /// <summary>
            /// Runs delegates inside a scoped {{model.Name}} ambient value.
            /// </summary>
            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "{{GeneratorVersion}}")]
            public interface {{contextInterfaceName}}
            {
                void {{executeMethodName}}({{valueType}} value, Action action);

                TResult {{executeMethodName}}<TResult>({{valueType}} value, Func<TResult> action);

                Task {{executeAsyncMethodName}}(
                    {{valueType}} value,
                    Func<CancellationToken, Task> action,
                    CancellationToken cancellationToken = default);

                Task<TResult> {{executeAsyncMethodName}}<TResult>(
                    {{valueType}} value,
                    Func<CancellationToken, Task<TResult>> action,
                    CancellationToken cancellationToken = default);
            }

            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "{{GeneratorVersion}}")]
            [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            internal sealed class {{contextName}} : {{contextInterfaceName}}
            {
                private readonly AmbientContextRuntime<{{markerName}}, {{valueType}}> _runtime;

                internal {{contextName}}(AmbientContextRuntime<{{markerName}}, {{valueType}}> runtime)
                {
                    _runtime = runtime;
                }

                public void {{executeMethodName}}({{valueType}} value, Action action)
                {
                    _runtime.Execute(value, action);
                }

                public TResult {{executeMethodName}}<TResult>({{valueType}} value, Func<TResult> action)
                {
                    return _runtime.Execute(value, action);
                }

                public Task {{executeAsyncMethodName}}(
                    {{valueType}} value,
                    Func<CancellationToken, Task> action,
                    CancellationToken cancellationToken = default)
                {
                    return _runtime.ExecuteAsync(value, action, cancellationToken);
                }

                public Task<TResult> {{executeAsyncMethodName}}<TResult>(
                    {{valueType}} value,
                    Func<CancellationToken, Task<TResult>> action,
                    CancellationToken cancellationToken = default)
                {
                    return _runtime.ExecuteAsync(value, action, cancellationToken);
                }
            }

            /// <summary>
            /// Provides dependency injection registration helpers for the {{model.Name}} ambient context.
            /// </summary>
            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "{{GeneratorVersion}}")]
            [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            public static class {{serviceCollectionExtensionsName}}
            {
                public static IServiceCollection Add{{model.Name}}Context(this IServiceCollection services)
                {
                    return {{model.Name}}AmbientContextRegistration.Add(services);
                }
            }

            file static class {{model.Name}}AmbientContextRegistration
            {
                public static IServiceCollection Add(IServiceCollection services)
                {
                    services.AddAmbientContext<{{markerName}}, {{valueType}}>("{{model.Name}}");
                    services.TryAddSingleton<{{accessorInterfaceName}}>(static provider =>
                        new {{accessorName}}(provider.GetRequiredService<AmbientContextRuntime<{{markerName}}, {{valueType}}>>()));
                    services.TryAddSingleton<{{contextInterfaceName}}>(static provider =>
                        new {{contextName}}(provider.GetRequiredService<AmbientContextRuntime<{{markerName}}, {{valueType}}>>()));

                    return services;
                }
            }
            """;
    }

    private static string GenerateRegistrationExtensions(
        ImmutableArray<AmbientContextModel> models,
        AmbientContextRegistrationModel registration)
    {
        var registrations = new StringBuilder();

        foreach (var model in models)
        {
            registrations.Append("            global::")
                .Append(model.Namespace)
                .Append('.')
                .Append(model.Name)
                .Append("AmbientContextServiceCollectionExtensions.Add")
                .Append(model.Name)
                .AppendLine("Context(services);");
        }

        return $$"""
            // <auto-generated />
            #nullable enable
            #pragma warning disable CS1591

            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;

            namespace Microsoft.Extensions.DependencyInjection
            {
                [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "{{GeneratorVersion}}")]
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                public static class {{registration.ServiceCollectionExtensionsName}}
                {
                    public static IServiceCollection {{registration.MethodName}}(this IServiceCollection services)
                    {
                        return AmbientContextRegistration.Add(services);
                    }
                }
            }

            namespace Microsoft.Extensions.Hosting
            {
                [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "{{GeneratorVersion}}")]
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                public static class {{registration.HostBuilderExtensionsName}}
                {
                    public static IHostBuilder {{registration.MethodName}}(this IHostBuilder hostBuilder)
                    {
                        hostBuilder.ConfigureServices(static (_, services) =>
                            global::Microsoft.Extensions.DependencyInjection.{{registration.ServiceCollectionExtensionsName}}.{{registration.MethodName}}(services));

                        return hostBuilder;
                    }
                }
            }

            file static class AmbientContextRegistration
            {
                public static IServiceCollection Add(IServiceCollection services)
                {
            {{registrations}}
                    return services;
                }
            }
            """;
    }

    private static string GenerateTryGetCurrent(
        AmbientContextModel model,
        string tryGetAttribute,
        string tryGetOutType)
    {
        if (!model.IsValueType)
        {
            return $$"""
                public bool TryGetCurrent({{tryGetAttribute}}out {{tryGetOutType}} value)
                {
                    return _runtime.TryGetCurrent(out value);
                }

            """;
        }

        return $$"""
                public bool TryGetCurrent(out {{tryGetOutType}} value)
                {
                    if (_runtime.TryGetCurrent(out var current))
                    {
                        value = current;
                        return true;
                    }

                    value = default;
                    return false;
                }

            """;
    }

    private static IEnumerable<string> GetGeneratedTypeNames(AmbientContextModel model)
    {
        yield return $"{model.Name}AmbientContextMarker";
        yield return $"I{model.Name}Accessor";
        yield return $"{model.Name}Accessor";
        yield return $"I{model.Name}Context";
        yield return $"{model.Name}Context";
        yield return $"{model.Name}AmbientContextServiceCollectionExtensions";
    }

    private static string GetRootNamespace(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return optionsProvider.GlobalOptions.TryGetValue(
                "build_property.RootNamespace",
                out var rootNamespace) &&
            !string.IsNullOrWhiteSpace(rootNamespace)
            ? rootNamespace!
            : "AmbientContext.Generated";
    }

    private static object? GetNamedArgument(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
            {
                return argument.Value.Value;
            }
        }

        return null;
    }

    private static Location? GetLocation(
        AttributeData attribute,
        CancellationToken cancellationToken)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation();
    }

    private static bool IsValidNamespace(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Split('.').All(SyntaxFacts.IsValidIdentifier);
    }

    private static bool IsComposableIdentifier(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value!.IndexOf('@') < 0 &&
            SyntaxFacts.IsValidIdentifier(value);
    }

    private static bool IsNullableValueType(ITypeSymbol symbol)
    {
        return symbol is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static bool ContainsTypeParameter(ITypeSymbol symbol)
    {
        if (symbol.TypeKind == TypeKind.TypeParameter)
        {
            return true;
        }

        return symbol is INamedTypeSymbol namedType &&
            (namedType.IsUnboundGenericType ||
                namedType.TypeArguments.Any(ContainsTypeParameter));
    }

    private static bool IsUnsupported(ITypeSymbol symbol)
    {
        if (symbol.SpecialType == SpecialType.System_Void ||
            symbol.TypeKind == TypeKind.Pointer ||
            symbol.TypeKind == TypeKind.FunctionPointer)
        {
            return true;
        }

        return symbol is INamedTypeSymbol { IsRefLikeType: true } ||
            symbol is INamedTypeSymbol { IsStatic: true } ||
            symbol.TypeKind == TypeKind.Module;
    }

    private sealed class GenerationOutput
    {
        public static readonly GenerationOutput Empty = new(
            ImmutableArray<GenerationItem>.Empty,
            null);

        public GenerationOutput(
            ImmutableArray<GenerationItem> items,
            AmbientContextRegistrationModel? registration)
        {
            Items = items;
            Registration = registration;
        }

        public ImmutableArray<GenerationItem> Items { get; }

        public AmbientContextRegistrationModel? Registration { get; }
    }

    private sealed class GenerationItem
    {
        private GenerationItem(AmbientContextModel? context, Diagnostic? diagnostic)
        {
            Context = context;
            Diagnostic = diagnostic;
        }

        public AmbientContextModel? Context { get; }

        public Diagnostic? Diagnostic { get; }

        public static GenerationItem FromContext(AmbientContextModel context)
        {
            return new GenerationItem(context, null);
        }

        public static GenerationItem FromDiagnostic(Diagnostic diagnostic)
        {
            return new GenerationItem(null, diagnostic);
        }
    }

    private sealed class AmbientContextDeclaration
    {
        private AmbientContextDeclaration(
            AmbientContextModel? context,
            Diagnostic? diagnostic,
            Location? location)
        {
            Context = context;
            Diagnostic = diagnostic;
            Location = location;
        }

        public AmbientContextModel? Context { get; }

        public Diagnostic? Diagnostic { get; }

        public Location? Location { get; }

        public static AmbientContextDeclaration Valid(
            string name,
            string targetNamespace,
            string valueTypeName,
            bool isValueType,
            Location? location)
        {
            return new AmbientContextDeclaration(
                new AmbientContextModel(name, targetNamespace, valueTypeName, isValueType),
                null,
                location);
        }

        public static AmbientContextDeclaration Invalid(Diagnostic diagnostic)
        {
            return new AmbientContextDeclaration(null, diagnostic, diagnostic.Location);
        }
    }

    private sealed class AmbientContextModel
    {
        public AmbientContextModel(
            string name,
            string @namespace,
            string valueTypeName,
            bool isValueType)
        {
            Name = name;
            Namespace = @namespace;
            ValueTypeName = valueTypeName;
            IsValueType = isValueType;
        }

        public string Name { get; }

        public string Namespace { get; }

        public string ValueTypeName { get; }

        public bool IsValueType { get; }
    }

    private sealed class AmbientContextRegistrationModel
    {
        public AmbientContextRegistrationModel(string methodName)
        {
            MethodName = methodName;
            ServiceCollectionExtensionsName = $"{methodName}ServiceCollectionExtensions";
            HostBuilderExtensionsName = $"{methodName}HostBuilderExtensions";
        }

        public string MethodName { get; }

        public string ServiceCollectionExtensionsName { get; }

        public string HostBuilderExtensionsName { get; }
    }
}
