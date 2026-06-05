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
