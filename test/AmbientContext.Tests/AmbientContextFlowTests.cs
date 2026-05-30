using AmbientContext.Core;
using AwesomeAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbientContext.Tests;

[TestClass]
public sealed class AmbientContextFlowTests
{
    [TestMethod]
    public void SuppressFor_executes_action()
    {
        var called = false;

        AmbientContextFlow.SuppressFor(() => called = true);

        called.Should().BeTrue();
    }

    [TestMethod]
    public void SuppressFor_returns_result()
    {
        var result = AmbientContextFlow.SuppressFor(() => 42);

        result.Should().Be(42);
    }
}
