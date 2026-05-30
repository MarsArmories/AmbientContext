# AmbientContext

AmbientContext is a .NET 10 library for strongly typed ambient values over `AsyncLocal<T>`. The runtime owns the scoped state, while the source generator creates named APIs such as `ClientIdContext`, `TenantIdContext`, and typed accessors.

## Basic Usage

Define contexts with assembly attributes:

```csharp
using AmbientContext.Abstractions;

[assembly: AmbientContext(typeof(Guid), "ClientId")]
[assembly: AmbientContext(typeof(string), "TenantId")]
```

Register the generated services:

```csharp
services.AddClientIdContext();
services.AddTenantIdContext();
```

Inject the generated context runner and accessor:

```csharp
public sealed class Worker
{
    private readonly ClientIdContext _clientIdContext;
    private readonly IClientIdAccessor _clientIdAccessor;

    public Worker(ClientIdContext clientIdContext, IClientIdAccessor clientIdAccessor)
    {
        _clientIdContext = clientIdContext;
        _clientIdAccessor = clientIdAccessor;
    }

    public Task RunAsync(Guid clientId, CancellationToken cancellationToken)
    {
        return _clientIdContext.ExecuteAsClientIdAsync(clientId, ct =>
        {
            Console.WriteLine(_clientIdAccessor.Current);
            return Task.CompletedTask;
        }, cancellationToken);
    }
}
```

## Reading Current

`Current` returns the value for the active ambient scope. Accessing `Current` outside a matching `ExecuteAs*Async` scope throws `AmbientContextMissingException`.

`TryGetCurrent` and `CurrentOrDefault` are available for optional reads. Generated value-type accessors expose nullable `CurrentOrDefault`, for example `Guid?`.

## Null Values

Ambient values are non-null. Passing `null` for a reference-type context throws `AmbientContextNullValueException`.

## Nested Scopes

Nested scopes restore the previous value when they complete. Different contexts are isolated by generated marker types, so `ClientId` and `CorrelationId` can both use `Guid` without colliding.

Exceptions inside `ExecuteAs*Async` do not leak context state. The previous value is restored through `finally`/`Dispose` behavior.

## ExecutionContext Flow

AmbientContext uses `AsyncLocal<T>`. `AsyncLocal<T>` values flow with .NET's `ExecutionContext`.

This means work started inside an `ExecuteAs*Async` scope may capture the current ambient value:

```csharp
await clientIdContext.ExecuteAsClientIdAsync(clientId, async ct =>
{
    _ = Task.Run(ProcessLaterAsync);
}, cancellationToken);
```

The queued task may still observe the `ClientId` even after `ExecuteAsClientIdAsync` has completed. This is expected .NET behavior.

For fire-and-forget work that must not inherit ambient context, suppress `ExecutionContext` flow:

```csharp
await clientIdContext.ExecuteAsClientIdAsync(clientId, async ct =>
{
    using (ExecutionContext.SuppressFlow())
    {
        _ = Task.Run(ProcessLaterAsync);
    }
}, cancellationToken);
```

`ExecutionContext.SuppressFlow` prevents `AsyncLocal<T>`, culture, security context, and other execution-context data from flowing into newly queued asynchronous work.

Only suppress flow around the scheduling operation.

Correct:

```csharp
using (ExecutionContext.SuppressFlow())
{
    _ = Task.Run(ProcessLaterAsync);
}
```

Incorrect:

```csharp
using (ExecutionContext.SuppressFlow())
{
    await SomethingAsync();
}
```

Suppressing flow across `await` can cause problems because `AsyncFlowControl` must be restored on the same logical flow where it was created.

Recommended helper:

```csharp
AmbientContextFlow.SuppressFor(() =>
{
    _ = Task.Run(ProcessLaterAsync);
});
```

## Analyzer Warnings

`AmbientContext.Analyzers` reports obvious fire-and-forget scheduling inside `ExecuteAs*Async` lambdas.

The current analyzer rule inventory is maintained in:

* `src/AmbientContext.Analyzers/AnalyzerReleases.Shipped.md`
* `src/AmbientContext.Analyzers/AnalyzerReleases.Unshipped.md`

Generator diagnostics are maintained in `src/AmbientContext.Generators/GeneratorDiagnostics.md`.

## Observability

AmbientContext does not emit OpenTelemetry spans, scopes, or metrics by default.

Ambient values are often tenant, client, user, correlation, or workflow identifiers. Automatically attaching those values to spans or meter tags can create high-cardinality telemetry, increase export cost, and accidentally expose sensitive application data.

Prefer adding metrics and spans at the application boundary where the ambient value has domain meaning. Consumer code can read generated accessors such as `IClientIdAccessor` and decide which values are safe to record, which should be hashed or bucketed, and which should never leave process memory.
