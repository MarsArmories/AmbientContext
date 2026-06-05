using Microsoft.CodeAnalysis;

namespace AmbientContext.Generators;

internal static class AmbientContextDiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidName = new(
        "MAACGEN001",
        "Invalid ambient context name",
        "Ambient context name '{0}' must be a valid C# identifier",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateName = new(
        "MAACGEN002",
        "Duplicate ambient context name",
        "Ambient context name '{0}' is declared more than once",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NullableValueType = new(
        "MAACGEN003",
        "Nullable value type is unsupported",
        "Ambient context '{0}' cannot use nullable value type '{1}'",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedValueType = new(
        "MAACGEN004",
        "Ambient context value type is unsupported",
        "Ambient context '{0}' cannot use unsupported value type '{1}'",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnresolvedValueType = new(
        "MAACGEN005",
        "Could not resolve ambient context value type",
        "Ambient context '{0}' value type could not be resolved",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidNamespace = new(
        "MAACGEN006",
        "Invalid ambient context namespace",
        "Namespace '{0}' for ambient context '{1}' must be a valid C# namespace",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OpenGenericValueType = new(
        "MAACGEN007",
        "Open generic ambient context value type is unsupported",
        "Ambient context '{0}' cannot use open generic value type '{1}'",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GeneratedNameCollision = new(
        "MAACGEN008",
        "Generated ambient context name collision",
        "AmbientContext cannot generate '{0}' because that type already exists",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateRegistration = new(
        "MAACGEN009",
        "Duplicate aggregate registration declaration",
        "AmbientContextRegistrationAttribute may only be declared once per assembly",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidRegistrationName = new(
        "MAACGEN010",
        "Invalid aggregate registration method name",
        "Aggregate registration method name '{0}' must be a valid C# identifier",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RegistrationNameCollision = new(
        "MAACGEN011",
        "Aggregate registration method name collision",
        "Aggregate registration method name '{0}' conflicts with a generated per-context registration method",
        "AmbientContext",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
