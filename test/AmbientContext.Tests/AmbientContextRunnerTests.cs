using AmbientContext.Abstractions;
using AmbientContext.Core;
using AwesomeAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace AmbientContext.Tests;

[TestClass]
public sealed class AmbientContextRunnerTests
{
    [TestMethod]
    public async Task ExecuteAsync_exposes_current_value()
    {
        var state = CreateState<ClientIdContextMarker, Guid>("ClientId");
        var runner = new AmbientContextRunner<ClientIdContextMarker, Guid>(state);
        var value = Guid.NewGuid();

        await runner.ExecuteAsync(value, _ =>
        {
            state.Current.Should().Be(value);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ExecuteAsync_restores_previous_value_after_completion()
    {
        var state = CreateState<ClientIdContextMarker, string>("TenantId");
        var runner = new AmbientContextRunner<ClientIdContextMarker, string>(state);

        await runner.ExecuteAsync("outer", (Func<CancellationToken, Task>)(async cancellationToken =>
        {
            await runner.ExecuteAsync("inner", (Func<CancellationToken, Task>)(_ =>
            {
                state.Current.Should().Be("inner");
                return Task.CompletedTask;
            }), cancellationToken);

            state.Current.Should().Be("outer");
        }));

        state.HasCurrent.Should().BeFalse();
    }

    [TestMethod]
    public async Task ExecuteAsync_restores_previous_value_after_exception()
    {
        var state = CreateState<ClientIdContextMarker, string>("TenantId");
        var runner = new AmbientContextRunner<ClientIdContextMarker, string>(state);

        var act = async () => await runner.ExecuteAsync(
            "outer",
            (Func<CancellationToken, Task>)(_ => throw new InvalidOperationException("boom")));

        await act.Should().ThrowAsync<InvalidOperationException>();
        state.HasCurrent.Should().BeFalse();
    }

    [TestMethod]
    public async Task Nested_different_contexts_are_independent()
    {
        var clientState = CreateState<ClientIdContextMarker, Guid>("ClientId");
        var tenantState = CreateState<TenantIdContextMarker, string>("TenantId");
        var clientRunner = new AmbientContextRunner<ClientIdContextMarker, Guid>(clientState);
        var tenantRunner = new AmbientContextRunner<TenantIdContextMarker, string>(tenantState);
        var clientId = Guid.NewGuid();

        await clientRunner.ExecuteAsync(clientId, (Func<CancellationToken, Task>)(async cancellationToken =>
        {
            await tenantRunner.ExecuteAsync("tenant-1", (Func<CancellationToken, Task>)(_ =>
            {
                clientState.Current.Should().Be(clientId);
                tenantState.Current.Should().Be("tenant-1");
                return Task.CompletedTask;
            }), cancellationToken);

            clientState.Current.Should().Be(clientId);
            tenantState.HasCurrent.Should().BeFalse();
        }));
    }

    [TestMethod]
    public async Task Same_value_type_contexts_are_independent()
    {
        var clientState = CreateState<ClientIdContextMarker, Guid>("ClientId");
        var correlationState = CreateState<CorrelationIdContextMarker, Guid>("CorrelationId");
        var clientRunner = new AmbientContextRunner<ClientIdContextMarker, Guid>(clientState);
        var correlationRunner = new AmbientContextRunner<CorrelationIdContextMarker, Guid>(correlationState);
        var clientId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        await clientRunner.ExecuteAsync(clientId, (Func<CancellationToken, Task>)(async cancellationToken =>
        {
            await correlationRunner.ExecuteAsync(correlationId, (Func<CancellationToken, Task>)(_ =>
            {
                clientState.Current.Should().Be(clientId);
                correlationState.Current.Should().Be(correlationId);
                return Task.CompletedTask;
            }), cancellationToken);
        }));
    }

    [TestMethod]
    public async Task CancellationToken_is_passed_to_delegate()
    {
        var state = CreateState<ClientIdContextMarker, string>("TenantId");
        var runner = new AmbientContextRunner<ClientIdContextMarker, string>(state);
        var observer = Substitute.For<ICancellationObserver>();
        using var cancellationTokenSource = new CancellationTokenSource();

        await runner.ExecuteAsync("tenant-1", ct =>
        {
            observer.Observe(ct);
            return Task.CompletedTask;
        }, cancellationTokenSource.Token);

        observer.Received(1).Observe(cancellationTokenSource.Token);
    }

    [TestMethod]
    public async Task Task_result_overload_returns_result()
    {
        var state = CreateState<ClientIdContextMarker, string>("TenantId");
        var runner = new AmbientContextRunner<ClientIdContextMarker, string>(state);

        var result = await runner.ExecuteAsync("tenant-1", _ => Task.FromResult(42));

        result.Should().Be(42);
    }

    [TestMethod]
    public async Task ValueTask_overload_executes()
    {
        var state = CreateState<ClientIdContextMarker, string>("TenantId");
        var runner = new AmbientContextRunner<ClientIdContextMarker, string>(state);
        var called = false;

        await runner.ExecuteAsync("tenant-1", _ =>
        {
            called = true;
            return ValueTask.CompletedTask;
        });

        called.Should().BeTrue();
    }

    [TestMethod]
    public async Task ValueTask_result_overload_returns_result()
    {
        var state = CreateState<ClientIdContextMarker, string>("TenantId");
        var runner = new AmbientContextRunner<ClientIdContextMarker, string>(state);

        var result = await runner.ExecuteAsync("tenant-1", _ => ValueTask.FromResult(42));

        result.Should().Be(42);
    }

    private static AmbientContextState<TContext, TValue> CreateState<TContext, TValue>(string name)
        where TContext : notnull
        where TValue : notnull
    {
        return new AmbientContextState<TContext, TValue>(new AmbientContextOptions<TContext, TValue>
        {
            ContextName = name
        });
    }

    public interface ICancellationObserver
    {
        void Observe(CancellationToken cancellationToken);
    }

    private sealed class ClientIdContextMarker
    {
    }

    private sealed class TenantIdContextMarker
    {
    }

    private sealed class CorrelationIdContextMarker
    {
    }
}
