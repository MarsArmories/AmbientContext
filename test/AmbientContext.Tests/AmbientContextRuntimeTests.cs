using AmbientContext.Abstractions;
using AmbientContext.Core;
using AwesomeAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbientContext.Tests;

[TestClass]
public sealed class AmbientContextRuntimeTests
{
    [TestMethod]
    public void Current_throws_when_value_is_missing()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, Guid>("ClientId");

        var act = () => runtime.Current;

        act.Should().Throw<AmbientContextMissingException>()
            .Which.ContextName.Should().Be("ClientId");
    }

    [TestMethod]
    public void Optional_reads_report_a_missing_value()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, string>("TenantId");

        runtime.HasCurrent.Should().BeFalse();
        runtime.CurrentOrDefault.Should().BeNull();
        runtime.TryGetCurrent(out var value).Should().BeFalse();
        value.Should().BeNull();
    }

    [TestMethod]
    public void Execute_exposes_current_value_and_restores_it()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, Guid>("ClientId");
        var value = Guid.NewGuid();

        runtime.Execute(value, () => runtime.Current.Should().Be(value));

        runtime.HasCurrent.Should().BeFalse();
    }

    [TestMethod]
    public void Execute_returns_result()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, string>("TenantId");

        var result = runtime.Execute("tenant-1", () => runtime.Current.Length);

        result.Should().Be(8);
    }

    [TestMethod]
    public void Execute_restores_previous_value_after_exception()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, string>("TenantId");

        var act = () => runtime.Execute("outer", () => throw new InvalidOperationException("boom"));

        act.Should().Throw<InvalidOperationException>();
        runtime.HasCurrent.Should().BeFalse();
    }

    [TestMethod]
    public async Task ExecuteAsync_accepts_an_async_lambda_without_a_cast()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, Guid>("ClientId");
        var value = Guid.NewGuid();

        await runtime.ExecuteAsync(value, async _ =>
        {
            await Task.Yield();
            runtime.Current.Should().Be(value);
        });

        runtime.HasCurrent.Should().BeFalse();
    }

    [TestMethod]
    public async Task ExecuteAsync_restores_previous_value_after_completion()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, string>("TenantId");

        await runtime.ExecuteAsync("outer", async cancellationToken =>
        {
            await runtime.ExecuteAsync("inner", _ =>
            {
                runtime.Current.Should().Be("inner");
                return Task.CompletedTask;
            }, cancellationToken);

            runtime.Current.Should().Be("outer");
        });

        runtime.HasCurrent.Should().BeFalse();
    }

    [TestMethod]
    public async Task ExecuteAsync_restores_previous_value_after_exception()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, string>("TenantId");

        var act = () => runtime.ExecuteAsync(
            "outer",
            _ => throw new InvalidOperationException("boom"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        runtime.HasCurrent.Should().BeFalse();
    }

    [TestMethod]
    public async Task Different_contexts_are_independent()
    {
        var clientRuntime = CreateRuntime<ClientIdContextMarker, Guid>("ClientId");
        var tenantRuntime = CreateRuntime<TenantIdContextMarker, string>("TenantId");
        var clientId = Guid.NewGuid();

        await clientRuntime.ExecuteAsync(clientId, async cancellationToken =>
        {
            await tenantRuntime.ExecuteAsync("tenant-1", _ =>
            {
                clientRuntime.Current.Should().Be(clientId);
                tenantRuntime.Current.Should().Be("tenant-1");
                return Task.CompletedTask;
            }, cancellationToken);

            clientRuntime.Current.Should().Be(clientId);
            tenantRuntime.HasCurrent.Should().BeFalse();
        });
    }

    [TestMethod]
    public async Task CancellationToken_is_passed_to_delegate()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, string>("TenantId");
        using var cancellationTokenSource = new CancellationTokenSource();
        var observed = default(CancellationToken);

        await runtime.ExecuteAsync("tenant-1", cancellationToken =>
        {
            observed = cancellationToken;
            return Task.CompletedTask;
        }, cancellationTokenSource.Token);

        observed.Should().Be(cancellationTokenSource.Token);
    }

    [TestMethod]
    public async Task Task_result_overload_returns_result()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, string>("TenantId");

        var result = await runtime.ExecuteAsync("tenant-1", _ => Task.FromResult(42));

        result.Should().Be(42);
    }

    [TestMethod]
    public void Null_reference_value_throws()
    {
        var runtime = CreateRuntime<ClientIdContextMarker, string>("TenantId");

        var act = () => runtime.Execute(null!, () => { });

        act.Should().Throw<AmbientContextNullValueException>()
            .Which.ContextName.Should().Be("TenantId");
    }

    private static AmbientContextRuntime<TContext, TValue> CreateRuntime<TContext, TValue>(string name)
        where TContext : notnull
        where TValue : notnull
    {
        return new AmbientContextRuntime<TContext, TValue>(name);
    }

    private sealed class ClientIdContextMarker
    {
    }

    private sealed class TenantIdContextMarker
    {
    }
}
