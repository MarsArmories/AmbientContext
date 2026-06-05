; Shipped analyzer releases
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MAAC0001 | AmbientContext | Warning | Fire-and-forget work created inside ambient context scope.
MAAC0002 | AmbientContext | Warning | Task.Run used inside ambient context scope without flow suppression.
MAAC0003 | AmbientContext | Warning | ThreadPool.QueueUserWorkItem used inside ambient context scope without flow suppression.
MAAC0004 | AmbientContext | Warning | Task.Factory.StartNew used inside ambient context scope without flow suppression.

## Release 2.0.0

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MAAC0001 | AmbientContext | Warning | Removed because no implementation reported this diagnostic.
