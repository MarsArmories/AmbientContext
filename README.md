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

`AmbientContext.Analyzers` reports obvious fire-and-forget scheduling inside `ExecuteAs*Async` lambdas:

* `AC0001`: Fire-and-forget work created inside ambient context scope.
* `AC0002`: `Task.Run` used inside ambient context scope without flow suppression.
* `AC0003`: `ThreadPool.QueueUserWorkItem` used inside ambient context scope without flow suppression.
* `AC0004`: `Task.Factory.StartNew` used inside ambient context scope without flow suppression.
