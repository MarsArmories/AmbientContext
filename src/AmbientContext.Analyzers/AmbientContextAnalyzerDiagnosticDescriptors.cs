using Microsoft.CodeAnalysis;

namespace AmbientContext.Analyzers;

internal static class AmbientContextAnalyzerDiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor TaskRun = new(
        "MAAC0002",
        "Task.Run used inside ambient context scope",
        "Task.Run inside an ambient context scope should be scheduled within ExecutionContext.SuppressFlow or AmbientContextFlow.SuppressFor",
        "AmbientContext",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ThreadPoolQueue = new(
        "MAAC0003",
        "ThreadPool work item queued inside ambient context scope",
        "ThreadPool.QueueUserWorkItem inside an ambient context scope should be scheduled within ExecutionContext.SuppressFlow or AmbientContextFlow.SuppressFor",
        "AmbientContext",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TaskFactoryStartNew = new(
        "MAAC0004",
        "Task.Factory.StartNew used inside ambient context scope",
        "Task.Factory.StartNew inside an ambient context scope should be scheduled within ExecutionContext.SuppressFlow or AmbientContextFlow.SuppressFor",
        "AmbientContext",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
