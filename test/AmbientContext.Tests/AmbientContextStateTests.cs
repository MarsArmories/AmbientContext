using AmbientContext.Abstractions;
using AmbientContext.Core;
using AwesomeAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbientContext.Tests;

[TestClass]
public sealed class AmbientContextStateTests
{
    [TestMethod]
    public void Current_throws_when_value_is_missing()
    {
        var accessor = CreateAccessor<ClientIdContextMarker, Guid>("ClientId");

        var act = () => accessor.Current;

        act.Should().Throw<AmbientContextMissingException>()
            .Which.ContextName.Should().Be("ClientId");
    }

    [TestMethod]
    public void TryGetCurrent_returns_false_when_value_is_missing()
    {
        var accessor = CreateAccessor<ClientIdContextMarker, string>("TenantId");

        var result = accessor.TryGetCurrent(out var value);

        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [TestMethod]
    public void CurrentOrDefault_returns_null_for_missing_reference_type()
    {
        var accessor = CreateAccessor<ClientIdContextMarker, string>("TenantId");

        accessor.CurrentOrDefault.Should().BeNull();
    }

    [TestMethod]
    public void Pushing_null_reference_value_throws()
    {
        var state = CreateState<ClientIdContextMarker, string>("TenantId");

        var act = () => state.Push(null!);

        act.Should().Throw<AmbientContextNullValueException>()
            .Which.ContextName.Should().Be("TenantId");
    }

    private static AmbientContextAccessor<TContext, TValue> CreateAccessor<TContext, TValue>(string name)
        where TContext : notnull
        where TValue : notnull
    {
        return new AmbientContextAccessor<TContext, TValue>(CreateState<TContext, TValue>(name));
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

    private sealed class ClientIdContextMarker
    {
    }
}
