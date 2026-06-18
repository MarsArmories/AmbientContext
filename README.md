# AmbientContext

[![CI](https://github.com/MarsArmories/AmbientContext/actions/workflows/ci.yml/badge.svg)](https://github.com/MarsArmories/AmbientContext/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/MarsArmories.AmbientContext.svg)](https://www.nuget.org/packages/MarsArmories.AmbientContext)

AmbientContext is a .NET 8 and .NET 10 library for strongly typed ambient values over `AsyncLocal<T>`. The runtime owns the scoped state, while the source generator creates named APIs such as `IClientIdContext`, `ITenantIdAccessor`, and optional aggregate registration helpers.

## Installation

Install the [MarsArmories.AmbientContext](https://www.nuget.org/packages/MarsArmories.AmbientContext) package:

```shell
dotnet package add MarsArmories.AmbientContext
```

The package includes the source generator.

Install the optional [MarsArmories.AmbientContext.Analyzers](https://www.nuget.org/packages/MarsArmories.AmbientContext.Analyzers) package for diagnostics around fire-and-forget work:

```shell
dotnet package add MarsArmories.AmbientContext.Analyzers
```

## Basic Usage

Define contexts with assembly attributes:

```csharp
using AmbientContext.Abstractions;

[assembly: AmbientContext(typeof(Guid), "ClientId")]
[assembly: AmbientContext(typeof(string), "TenantId")]
[assembly: AmbientContextRegistration("AddMyAppAmbientContexts")]
```

Register the generated services:

```csharp
services.AddMyAppAmbientContexts();
```

The optional `AmbientContextRegistration` attribute generates one aggregate registration method for the assembly. When the attribute is omitted, no aggregate method is generated. Individual methods such as `AddClientIdContext()` and `AddTenantIdContext()` are always available.

Generic host applications can register all generated contexts directly on `IHostBuilder`:

```csharp
Host.CreateDefaultBuilder(args)
    .AddMyAppAmbientContexts();
```

Inject the generated context and accessor interfaces:

```csharp
public sealed class Worker
{
    private readonly IClientIdContext _clientIdContext;
    private readonly IClientIdAccessor _clientIdAccessor;

    public Worker(IClientIdContext clientIdContext, IClientIdAccessor clientIdAccessor)
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

Generated context interfaces also provide synchronous `ExecuteAs*` overloads for `Action` and `Func<TResult>`. Asynchronous execution accepts Task-returning delegates, so ordinary `async` lambdas compile without delegate casts.

## Reading Current

`Current` returns the value for the active ambient scope. Accessing `Current` outside a matching `ExecuteAs*` or `ExecuteAs*Async` scope throws `AmbientContextMissingException`.

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

* [Shipped analyzer rules](https://github.com/MarsArmories/AmbientContext/blob/main/src/AmbientContext.Analyzers/AnalyzerReleases.Shipped.md)
* [Unshipped analyzer rules](https://github.com/MarsArmories/AmbientContext/blob/main/src/AmbientContext.Analyzers/AnalyzerReleases.Unshipped.md)

Generator diagnostics are maintained in the [generator diagnostics inventory](https://github.com/MarsArmories/AmbientContext/blob/main/src/AmbientContext.Generators/GeneratorDiagnostics.md).

* [Shipped generator diagnostics](https://github.com/MarsArmories/AmbientContext/blob/main/src/AmbientContext.Generators/AnalyzerReleases.Shipped.md)
* [Unshipped generator diagnostics](https://github.com/MarsArmories/AmbientContext/blob/main/src/AmbientContext.Generators/AnalyzerReleases.Unshipped.md)

## Migrating From 1.x

Version 2.0 intentionally reduces the public API surface:

* Inject generated `I{Name}Context` interfaces instead of concrete `{Name}Context` classes.
* Replace `AddAmbientContext()` with selective `Add{Name}Context()` calls, or opt into a uniquely named aggregate method with `AmbientContextRegistrationAttribute`.
* Replace `ValueTask` delegates with Task-returning delegates.
* Use `AmbientContextRuntime<TContext, TValue>` when directly consuming the low-level runtime API.

## Observability

AmbientContext does not emit OpenTelemetry spans, scopes, or metrics by default.

Ambient values are often tenant, client, user, correlation, or workflow identifiers. Automatically attaching those values to spans or meter tags can create high-cardinality telemetry, increase export cost, and accidentally expose sensitive application data.

Prefer adding metrics and spans at the application boundary where the ambient value has domain meaning. Consumer code can read generated accessors such as `IClientIdAccessor` and decide which values are safe to record, which should be hashed or bucketed, and which should never leave process memory.

## Integration Patterns

See the [integration patterns guide](docs/integration-patterns.md) for:

* ASP.NET Core middleware and endpoint filters.
* GraphQL request and resolver middleware.
* GraphQL DataLoader batching considerations.
* Message envelope and parallel-consumer patterns.
* Multiple ambient values and fire-and-forget work.

## Resources

* [GitHub repository](https://github.com/MarsArmories/AmbientContext)
* [NuGet package](https://www.nuget.org/packages/MarsArmories.AmbientContext)
* [Sample project](https://github.com/MarsArmories/AmbientContext/tree/main/samples/AmbientContext.Sample)
* [ASP.NET Core sample](https://github.com/MarsArmories/AmbientContext/tree/main/samples/AmbientContext.AspNetCore.Sample)
* [Messaging sample](https://github.com/MarsArmories/AmbientContext/tree/main/samples/AmbientContext.Messaging.Sample)
* [Changelog](https://github.com/MarsArmories/AmbientContext/blob/main/CHANGELOG.md)
* [Security policy](https://github.com/MarsArmories/AmbientContext/blob/main/SECURITY.md)
* [MIT license](https://github.com/MarsArmories/AmbientContext/blob/main/LICENSE)

Please use [GitHub Issues](https://github.com/MarsArmories/AmbientContext/issues) for bugs and feature requests. Report security vulnerabilities privately as described in the [security policy](https://github.com/MarsArmories/AmbientContext/blob/main/SECURITY.md).
