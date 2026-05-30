using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AmbientContext.Generators;

/// <summary>
/// Generates named ambient context APIs from assembly-level <c>AmbientContextAttribute</c> declarations.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AmbientContextGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "AmbientContext.Abstractions.AmbientContextAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generationItems = context.CompilationProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .SelectMany(static (source, cancellationToken) =>
                Collect(source.Left, source.Right, cancellationToken));

        context.RegisterSourceOutput(generationItems, static (sourceProductionContext, item) =>
        {
            if (item.Diagnostic is not null)
            {
                sourceProductionContext.ReportDiagnostic(item.Diagnostic);
                return;
            }

            sourceProductionContext.AddSource(
                $"AmbientContext_{item.Context!.Name}.g.cs",
                SourceText.From(Generate(item.Context), Encoding.UTF8));
        });
    }

    private static ImmutableArray<GenerationItem> Collect(
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        var attributeSymbol = compilation.GetTypeByMetadataName(AttributeMetadataName);

        if (attributeSymbol is null)
        {
            return ImmutableArray<GenerationItem>.Empty;
        }

        var rootNamespace = GetRootNamespace(optionsProvider);
        var declarations = new List<AmbientContextDeclaration>();

        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
            {
                continue;
            }

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

            if (string.IsNullOrWhiteSpace(name) || !SyntaxFacts.IsValidIdentifier(name))
            {
                declarations.Add(AmbientContextDeclaration.Invalid(
                    Diagnostic.Create(
                        AmbientContextDiagnosticDescriptors.InvalidName,
                        location,
                        name ?? string.Empty)));
                continue;
            }

            if (valueType is null || valueType.TypeKind == TypeKind.Error)
            {
                declarations.Add(AmbientContextDeclaration.Invalid(
                    Diagnostic.Create(
                        AmbientContextDiagnosticDescriptors.UnresolvedValueType,
                        location,
                        name)));
                continue;
            }

            if (IsNullableValueType(valueType))
            {
                declarations.Add(AmbientContextDeclaration.Invalid(
                    Diagnostic.Create(
                        AmbientContextDiagnosticDescriptors.NullableValueType,
                        location,
                        name,
                        valueType.ToDisplayString())));
                continue;
            }

            if (IsUnsupported(valueType))
            {
                declarations.Add(AmbientContextDeclaration.Invalid(
                    Diagnostic.Create(
                        AmbientContextDiagnosticDescriptors.UnsupportedValueType,
                        location,
                        name,
                        valueType.ToDisplayString())));
                continue;
            }

            declarations.Add(AmbientContextDeclaration.Valid(
                name,
                targetNamespace,
                valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                valueType.IsValueType,
                location));
        }

        var items = ImmutableArray.CreateBuilder<GenerationItem>();

        foreach (var declaration in declarations.Where(static declaration => declaration.Diagnostic is not null))
        {
            items.Add(GenerationItem.FromDiagnostic(declaration.Diagnostic!));
        }

        foreach (var duplicateGroup in declarations
            .Where(static declaration => declaration.Context is not null)
            .GroupBy(static declaration => declaration.Context!.Name)
            .Where(static group => group.Count() > 1))
        {
            foreach (var duplicate in duplicateGroup)
            {
                items.Add(GenerationItem.FromDiagnostic(Diagnostic.Create(
                    AmbientContextDiagnosticDescriptors.DuplicateName,
                    duplicate.Location,
                    duplicate.Context!.Name)));
            }
        }

        var duplicateNames = new HashSet<string>(
            declarations
                .Where(static declaration => declaration.Context is not null)
                .GroupBy(static declaration => declaration.Context!.Name)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key),
            StringComparer.Ordinal);

        foreach (var declaration in declarations.Where(static declaration => declaration.Context is not null))
        {
            if (!duplicateNames.Contains(declaration.Context!.Name))
            {
                items.Add(GenerationItem.FromContext(declaration.Context));
            }
        }

        return items.ToImmutable();
    }

    private static string Generate(AmbientContextModel model)
    {
        var markerName = $"{model.Name}AmbientContextMarker";
        var accessorInterfaceName = $"I{model.Name}Accessor";
        var accessorName = $"{model.Name}Accessor";
        var contextName = $"{model.Name}Context";
        var serviceCollectionExtensionsName = $"{model.Name}AmbientContextServiceCollectionExtensions";
        var executeMethodName = $"ExecuteAs{model.Name}Async";
        var tryGetAttribute = model.IsValueType ? string.Empty : "[NotNullWhen(true)] ";
        var tryGetOutType = model.IsValueType ? model.ValueTypeName : $"{model.ValueTypeName}?";
        var valueCurrentOrDefaultType = model.IsValueType ? $"{model.ValueTypeName}?" : $"{model.ValueTypeName}?";
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

            namespace {{model.Namespace}};

            /// <summary>
            /// Marker type used to isolate {{model.Name}} ambient context state.
            /// </summary>
            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "0.1.0")]
            [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            public sealed class {{markerName}}
            {
            }

            /// <summary>
            /// Reads the current {{model.Name}} ambient value.
            /// </summary>
            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "0.1.0")]
            public interface {{accessorInterfaceName}}
            {
                /// <summary>
                /// Gets a value indicating whether a current ambient value exists.
                /// </summary>
                bool HasCurrent { get; }

                /// <summary>
                /// Gets the current ambient value.
                /// </summary>
                {{valueType}} Current { get; }

                /// <summary>
                /// Gets the current ambient value, or null when no value is available.
                /// </summary>
                {{valueCurrentOrDefaultType}} CurrentOrDefault { get; }

                /// <summary>
                /// Attempts to get the current ambient value.
                /// </summary>
                /// <param name="value">The current ambient value when one is available.</param>
                /// <returns>true when a value is available; otherwise, false.</returns>
                bool TryGetCurrent({{tryGetAttribute}}out {{tryGetOutType}} value);
            }

            /// <summary>
            /// Default implementation of <see cref="{{accessorInterfaceName}}"/>.
            /// </summary>
            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "0.1.0")]
            [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            public sealed class {{accessorName}} : {{accessorInterfaceName}}
            {
                private readonly AmbientContextAccessor<{{markerName}}, {{valueType}}> _inner;

                /// <summary>
                /// Initializes a new instance of the <see cref="{{accessorName}}"/> class.
                /// </summary>
                /// <param name="inner">The inner ambient context accessor.</param>
                public {{accessorName}}(AmbientContextAccessor<{{markerName}}, {{valueType}}> inner)
                {
                    _inner = inner;
                }

                /// <inheritdoc />
                public bool HasCurrent => _inner.HasCurrent;

                /// <inheritdoc />
                public {{valueType}} Current => _inner.Current;

                /// <inheritdoc />
                public {{valueCurrentOrDefaultType}} CurrentOrDefault => _inner.CurrentOrDefault;

            {{GenerateTryGetCurrent(model, tryGetAttribute, tryGetOutType)}}
            }

            /// <summary>
            /// Runs delegates inside a scoped {{model.Name}} ambient value.
            /// </summary>
            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "0.1.0")]
            [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            public sealed class {{contextName}}
            {
                private readonly AmbientContextRunner<{{markerName}}, {{valueType}}> _runner;

                /// <summary>
                /// Initializes a new instance of the <see cref="{{contextName}}"/> class.
                /// </summary>
                /// <param name="runner">The inner ambient context runner.</param>
                public {{contextName}}(AmbientContextRunner<{{markerName}}, {{valueType}}> runner)
                {
                    _runner = runner;
                }

                /// <summary>
                /// Executes an asynchronous delegate inside a {{model.Name}} ambient context scope.
                /// </summary>
                /// <param name="value">The non-null ambient value.</param>
                /// <param name="action">The delegate to execute.</param>
                /// <param name="cancellationToken">A cancellation token passed to the delegate.</param>
                /// <returns>A task that represents the asynchronous operation.</returns>
                public Task {{executeMethodName}}(
                    {{valueType}} value,
                    Func<CancellationToken, Task> action,
                    CancellationToken cancellationToken = default)
                {
                    return _runner.ExecuteAsync(value, action, cancellationToken);
                }

                /// <summary>
                /// Executes an asynchronous delegate inside a {{model.Name}} ambient context scope and returns its result.
                /// </summary>
                /// <typeparam name="TResult">The delegate result type.</typeparam>
                /// <param name="value">The non-null ambient value.</param>
                /// <param name="action">The delegate to execute.</param>
                /// <param name="cancellationToken">A cancellation token passed to the delegate.</param>
                /// <returns>A task that contains the delegate result.</returns>
                public Task<TResult> {{executeMethodName}}<TResult>(
                    {{valueType}} value,
                    Func<CancellationToken, Task<TResult>> action,
                    CancellationToken cancellationToken = default)
                {
                    return _runner.ExecuteAsync(value, action, cancellationToken);
                }

                /// <summary>
                /// Executes a value-task delegate inside a {{model.Name}} ambient context scope.
                /// </summary>
                /// <param name="value">The non-null ambient value.</param>
                /// <param name="action">The delegate to execute.</param>
                /// <param name="cancellationToken">A cancellation token passed to the delegate.</param>
                /// <returns>A value task that represents the asynchronous operation.</returns>
                public ValueTask {{executeMethodName}}(
                    {{valueType}} value,
                    Func<CancellationToken, ValueTask> action,
                    CancellationToken cancellationToken = default)
                {
                    return _runner.ExecuteAsync(value, action, cancellationToken);
                }

                /// <summary>
                /// Executes a value-task delegate inside a {{model.Name}} ambient context scope and returns its result.
                /// </summary>
                /// <typeparam name="TResult">The delegate result type.</typeparam>
                /// <param name="value">The non-null ambient value.</param>
                /// <param name="action">The delegate to execute.</param>
                /// <param name="cancellationToken">A cancellation token passed to the delegate.</param>
                /// <returns>A value task that contains the delegate result.</returns>
                public ValueTask<TResult> {{executeMethodName}}<TResult>(
                    {{valueType}} value,
                    Func<CancellationToken, ValueTask<TResult>> action,
                    CancellationToken cancellationToken = default)
                {
                    return _runner.ExecuteAsync(value, action, cancellationToken);
                }
            }

            /// <summary>
            /// Provides dependency injection registration helpers for the {{model.Name}} ambient context.
            /// </summary>
            [global::System.CodeDom.Compiler.GeneratedCode("AmbientContext.Generators", "0.1.0")]
            [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
            public static class {{serviceCollectionExtensionsName}}
            {
                /// <summary>
                /// Registers the {{model.Name}} ambient context services.
                /// </summary>
                /// <param name="services">The service collection.</param>
                /// <returns>The same service collection.</returns>
                public static IServiceCollection Add{{model.Name}}Context(this IServiceCollection services)
                {
                    services.AddAmbientContext<{{markerName}}, {{valueType}}>("{{model.Name}}");
                    services.AddSingleton<{{accessorInterfaceName}}, {{accessorName}}>();
                    services.AddSingleton<{{contextName}}>();

                    return services;
                }
            }
            """;
    }

    private static string GenerateTryGetCurrent(AmbientContextModel model, string tryGetAttribute, string tryGetOutType)
    {
        if (!model.IsValueType)
        {
            return $$"""
                /// <inheritdoc />
                public bool TryGetCurrent({{tryGetAttribute}}out {{tryGetOutType}} value)
                {
                    return _inner.TryGetCurrent(out value);
                }

            """;
        }

        return $$"""
                /// <inheritdoc />
                public bool TryGetCurrent(out {{tryGetOutType}} value)
                {
                    if (_inner.TryGetCurrent(out var current))
                    {
                        value = current;
                        return true;
                    }

                    value = default;
                    return false;
                }

            """;
    }

    private static string GetRootNamespace(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return optionsProvider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace) &&
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

    private static Location? GetLocation(AttributeData attribute, CancellationToken cancellationToken)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation();
    }

    private static bool IsNullableValueType(ITypeSymbol symbol)
    {
        return symbol is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static bool IsUnsupported(ITypeSymbol symbol)
    {
        if (symbol.TypeKind == TypeKind.Pointer || symbol.TypeKind == TypeKind.FunctionPointer)
        {
            return true;
        }

        return symbol is INamedTypeSymbol { IsRefLikeType: true };
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
        private AmbientContextDeclaration(AmbientContextModel? context, Diagnostic? diagnostic, Location? location)
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
        public AmbientContextModel(string name, string @namespace, string valueTypeName, bool isValueType)
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
}
