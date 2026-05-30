using Microsoft.CodeAnalysis;

namespace AmbientContext.Generators;

internal static class AmbientContextDiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidName = new(
        "ACGEN001",
        "Invalid ambient context name",
        "Ambient context name '{0}' must be a valid C# identifier",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateName = new(
        "ACGEN002",
        "Duplicate ambient context name",
        "Ambient context name '{0}' is declared more than once",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NullableValueType = new(
        "ACGEN003",
        "Nullable value type is unsupported",
        "Ambient context '{0}' cannot use nullable value type '{1}'",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedValueType = new(
        "ACGEN004",
        "Ambient context value type is unsupported",
        "Ambient context '{0}' cannot use unsupported value type '{1}'",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnresolvedValueType = new(
        "ACGEN005",
        "Could not resolve ambient context value type",
        "Ambient context '{0}' value type could not be resolved",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
