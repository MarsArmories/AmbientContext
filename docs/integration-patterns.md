# Integration Patterns

AmbientContext works best at application boundaries. Extract and validate a value from the incoming request or message, then wrap the complete downstream operation in the generated `ExecuteAs*Async` method.

```csharp
await clientIdContext.ExecuteAsClientIdAsync(
    clientId,
    cancellationToken => HandleAsync(request, cancellationToken),
    cancellationToken);
```

The delegate-based API keeps scope lifetime explicit and guarantees that the previous value is restored after completion or failure.

## ASP.NET Core Middleware

Middleware is a good fit when a value comes from a header or another request-wide source.

```csharp
public async Task InvokeAsync(
    HttpContext httpContext,
    IClientIdContext clientIdContext)
{
    if (!Guid.TryParse(
        httpContext.Request.Headers["X-Client-Id"],
        out var clientId))
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    await clientIdContext.ExecuteAsClientIdAsync(
        clientId,
        _ => _next(httpContext),
        httpContext.RequestAborted);
}
```

Place the middleware after components needed to authenticate or normalize the value, and before components that read the generated accessor.

Treat headers as untrusted input. Authentication and authorization should establish whether the caller may act for the supplied tenant, client, or user identifier.

The sample registers `ClientIdHeaderMiddleware` globally with
`app.UseMiddleware<ClientIdHeaderMiddleware>()`. Its route-filter endpoint then
demonstrates a nested, route-derived override within that request-wide context.
See `samples/AmbientContext.AspNetCore.Sample`.

## ASP.NET Core Endpoint Filters

Endpoint filters are useful when the value comes from route values, bound arguments, or a parsed request body. The filter can read the bound value and wrap the remainder of the endpoint pipeline.

```csharp
public async ValueTask<object?> InvokeAsync(
    EndpointFilterInvocationContext invocationContext,
    EndpointFilterDelegate next)
{
    var clientId = invocationContext.GetArgument<Guid>(0);

    return await _clientIdContext.ExecuteAsClientIdAsync(
        clientId,
        _ => next(invocationContext).AsTask(),
        invocationContext.HttpContext.RequestAborted);
}
```

The generated AmbientContext API remains Task-based. When a framework callback returns `ValueTask<T>`, adapt that framework result with `AsTask()` at the boundary.

## GraphQL

GraphQL request or resolver middleware follows the same pattern:

1. Read the identifier from authenticated request state, arguments, or the operation context.
2. Validate authorization before establishing the ambient value.
3. Wrap the complete downstream request or resolver delegate in `ExecuteAs*Async`.

Request middleware is appropriate when one value applies to the entire operation. Resolver middleware is more appropriate when different fields may establish different values.

Be careful with parallel resolver execution. Ambient values flow through the captured `ExecutionContext`, but a value should never be mutated and assumed to affect sibling work that has already been scheduled.

## GraphQL DataLoaders

DataLoaders batch work that may originate from multiple resolvers. Do not use an ambient value as an invisible part of a DataLoader cache or batch key.

If tenant or client identity affects the result:

* Include it explicitly in the DataLoader key.
* Partition loaders by that identity.
* Pass it explicitly to the batch operation.

The batch operation may establish an ambient context around its own downstream work, but only after verifying that every item in the batch belongs to the same logical context.

## Message-Driven Systems

Establish ambient values once per delivered message from trusted envelope metadata, headers, keys, or validated body fields.

```csharp
await clientIdContext.ExecuteAsClientIdAsync(
    message.ClientId,
    cancellationToken => handler.HandleAsync(message, cancellationToken),
    cancellationToken);
```

Parallel message processing is supported because each asynchronous operation captures its own `ExecutionContext`. Do not establish one ambient scope around a loop that schedules work for messages belonging to different clients.

Prefer this:

```csharp
await Task.WhenAll(messages.Select(message =>
    ProcessMessageAsync(message, cancellationToken)));
```

Each `ProcessMessageAsync` call should establish its own ambient context. See `samples/AmbientContext.Messaging.Sample`.

## Multiple Ambient Values

Nest generated context methods when an operation needs more than one value:

```csharp
await clientIdContext.ExecuteAsClientIdAsync(clientId, cancellationToken =>
    tenantIdContext.ExecuteAsTenantIdAsync(
        tenantId,
        innerCancellationToken => handler.HandleAsync(innerCancellationToken),
        cancellationToken),
    cancellationToken);
```

Each context restores independently.

## Fire-And-Forget Work

Work scheduled inside an ambient scope captures the current `ExecutionContext` by default. For detached work that must not inherit ambient values, suppress flow only around the scheduling operation:

```csharp
AmbientContextFlow.SuppressFor(() =>
{
    _ = Task.Run(ProcessLaterAsync);
});
```

Do not suppress flow across an `await`.
