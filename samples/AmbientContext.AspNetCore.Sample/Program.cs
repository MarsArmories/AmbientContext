using AmbientContext.Abstractions;
using AmbientContext.AspNetCore.Sample;

[assembly: AmbientContext(
    typeof(Guid),
    "ClientId",
    Namespace = "AmbientContext.AspNetCore.Sample")]
[assembly: AmbientContextRegistration("AddAspNetCoreSampleAmbientContexts")]

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAspNetCoreSampleAmbientContexts();

var app = builder.Build();

app.UseMiddleware<ClientIdHeaderMiddleware>();

app.MapGet(
    "/header",
    (IClientIdAccessor clientIdAccessor) =>
        Results.Ok(new { ClientId = clientIdAccessor.Current }));

app.MapGet(
        "/clients/{clientId:guid}",
        (Guid clientId, IClientIdAccessor clientIdAccessor) =>
            Results.Ok(new
            {
                RouteClientId = clientId,
                AmbientClientId = clientIdAccessor.Current
            }))
    .AddEndpointFilter<ClientIdRouteFilter>();

app.Run();

internal sealed class ClientIdHeaderMiddleware
{
    private readonly RequestDelegate _next;

    public ClientIdHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        IClientIdContext clientIdContext)
    {
        if (!Guid.TryParse(
            httpContext.Request.Headers["X-Client-Id"],
            out var clientId))
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsync(
                "A valid X-Client-Id header is required.",
                httpContext.RequestAborted);
            return;
        }

        await clientIdContext.ExecuteAsClientIdAsync(
            clientId,
            _ => _next(httpContext),
            httpContext.RequestAborted);
    }
}

internal sealed class ClientIdRouteFilter : IEndpointFilter
{
    private readonly IClientIdContext _clientIdContext;

    public ClientIdRouteFilter(IClientIdContext clientIdContext)
    {
        _clientIdContext = clientIdContext;
    }

    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        var clientId = invocationContext.GetArgument<Guid>(0);

        return new ValueTask<object?>(
            _clientIdContext.ExecuteAsClientIdAsync(
                clientId,
                _ => next(invocationContext).AsTask(),
                invocationContext.HttpContext.RequestAborted));
    }
}
