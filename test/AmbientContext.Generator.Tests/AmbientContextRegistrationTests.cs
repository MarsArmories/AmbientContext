using AmbientContext.Abstractions;
using AmbientContext.Generator.Tests.Generated;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: AmbientContext(typeof(Guid), "GeneratedClientId", Namespace = "AmbientContext.Generator.Tests.Generated")]
[assembly: AmbientContext(typeof(string), "GeneratedTenantId", Namespace = "AmbientContext.Generator.Tests.Generated")]

namespace AmbientContext.Generator.Tests;

[TestClass]
public sealed class AmbientContextRegistrationTests
{
    [TestMethod]
    public void AddAmbientContext_registers_all_generated_contexts_on_service_collection()
    {
        var services = new ServiceCollection();

        services.AddAmbientContext();

        using var provider = services.BuildServiceProvider();

        provider.GetService<IGeneratedClientIdAccessor>().Should().NotBeNull();
        provider.GetService<GeneratedClientIdContext>().Should().NotBeNull();
        provider.GetService<IGeneratedTenantIdAccessor>().Should().NotBeNull();
        provider.GetService<GeneratedTenantIdContext>().Should().NotBeNull();
    }

    [TestMethod]
    public void AddAmbientContext_registers_all_generated_contexts_on_host_builder()
    {
        var hostBuilder = new HostBuilder();

        hostBuilder.AddAmbientContext();

        using var host = hostBuilder.Build();

        host.Services.GetService<IGeneratedClientIdAccessor>().Should().NotBeNull();
        host.Services.GetService<GeneratedClientIdContext>().Should().NotBeNull();
        host.Services.GetService<IGeneratedTenantIdAccessor>().Should().NotBeNull();
        host.Services.GetService<GeneratedTenantIdContext>().Should().NotBeNull();
    }
}
