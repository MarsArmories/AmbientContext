using AmbientContext.Abstractions;
using AmbientContext.Generator.Tests.Generated;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: AmbientContext(typeof(Guid), "GeneratedClientId", Namespace = "AmbientContext.Generator.Tests.Generated")]
[assembly: AmbientContext(typeof(string), "GeneratedTenantId", Namespace = "AmbientContext.Generator.Tests.Generated")]
[assembly: AmbientContextRegistration("AddGeneratorTestAmbientContexts")]

namespace AmbientContext.Generator.Tests;

[TestClass]
public sealed class AmbientContextRegistrationTests
{
    [TestMethod]
    public void Configured_aggregate_registers_all_generated_contexts_idempotently()
    {
        var services = new ServiceCollection();

        services.AddGeneratorTestAmbientContexts();
        services.AddGeneratorTestAmbientContexts();
        services.AddGeneratedClientIdContext();

        using var provider = services.BuildServiceProvider();

        provider.GetService<IGeneratedClientIdAccessor>().Should().NotBeNull();
        provider.GetService<IGeneratedClientIdContext>().Should().NotBeNull();
        provider.GetService<IGeneratedTenantIdAccessor>().Should().NotBeNull();
        provider.GetService<IGeneratedTenantIdContext>().Should().NotBeNull();
        provider.GetServices<IGeneratedClientIdAccessor>().Should().ContainSingle();
        provider.GetServices<IGeneratedClientIdContext>().Should().ContainSingle();
    }

    [TestMethod]
    public void Configured_aggregate_registers_all_generated_contexts_on_host_builder()
    {
        var hostBuilder = new HostBuilder();

        hostBuilder.AddGeneratorTestAmbientContexts();

        using var host = hostBuilder.Build();

        host.Services.GetService<IGeneratedClientIdAccessor>().Should().NotBeNull();
        host.Services.GetService<IGeneratedClientIdContext>().Should().NotBeNull();
        host.Services.GetService<IGeneratedTenantIdAccessor>().Should().NotBeNull();
        host.Services.GetService<IGeneratedTenantIdContext>().Should().NotBeNull();
    }

    [TestMethod]
    public async Task Generated_context_supports_sync_and_async_execution()
    {
        var services = new ServiceCollection();
        services.AddGeneratedClientIdContext();

        using var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<IGeneratedClientIdContext>();
        var accessor = provider.GetRequiredService<IGeneratedClientIdAccessor>();
        var clientId = Guid.NewGuid();

        var syncResult = context.ExecuteAsGeneratedClientId(clientId, () => accessor.Current);
        var asyncResult = await context.ExecuteAsGeneratedClientIdAsync(clientId, async _ =>
        {
            await Task.Yield();
            return accessor.Current;
        });

        syncResult.Should().Be(clientId);
        asyncResult.Should().Be(clientId);
        accessor.HasCurrent.Should().BeFalse();
    }
}
