; Unshipped generator diagnostic release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MAACGEN006 | AmbientContext | Error | Invalid generated namespace.
MAACGEN007 | AmbientContext | Error | Open generic value type is unsupported.
MAACGEN008 | AmbientContext | Error | Generated type name collides with an existing type.
MAACGEN009 | AmbientContext | Error | Aggregate registration is declared more than once.
MAACGEN010 | AmbientContext | Error | Invalid aggregate registration method name.
MAACGEN011 | AmbientContext | Error | Aggregate registration method name conflicts with a per-context method.
