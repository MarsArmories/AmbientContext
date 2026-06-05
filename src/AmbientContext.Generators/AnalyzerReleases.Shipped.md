; Shipped generator diagnostic releases
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MAACGEN001 | AmbientContext | Error | Invalid ambient context name.
MAACGEN002 | AmbientContext | Error | Duplicate ambient context name.
MAACGEN003 | AmbientContext | Error | Nullable value type is unsupported.
MAACGEN004 | AmbientContext | Error | Ambient context value type is unsupported.
MAACGEN005 | AmbientContext | Error | Ambient context value type could not be resolved.

## Release 2.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MAACGEN006 | AmbientContext | Error | Invalid generated namespace.
MAACGEN007 | AmbientContext | Error | Open generic value type is unsupported.
MAACGEN008 | AmbientContext | Error | Generated type name collides with an existing type.
MAACGEN009 | AmbientContext | Error | Aggregate registration is declared more than once.
MAACGEN010 | AmbientContext | Error | Invalid aggregate registration method name.
MAACGEN011 | AmbientContext | Error | Aggregate registration method name conflicts with a per-context method.
