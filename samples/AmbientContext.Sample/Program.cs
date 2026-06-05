using AmbientContext.Abstractions;
using Microsoft.Extensions.DependencyInjection;

[assembly: AmbientContext(typeof(Guid), "ClientId", Namespace = "AmbientContext.Sample")]
[assembly: AmbientContext(typeof(string), "TenantId", Namespace = "AmbientContext.Sample")]
[assembly: AmbientContextRegistration("AddSampleAmbientContexts")]

namespace AmbientContext.Sample;

public static class Program
{
    public static async Task Main()
    {
        var services = new ServiceCollection();

        services.AddSampleAmbientContexts();
        services.AddSingleton<Processor>();

        var provider = services.BuildServiceProvider();

        var clientIdContext = provider.GetRequiredService<IClientIdContext>();
        var processor = provider.GetRequiredService<Processor>();

        await clientIdContext.ExecuteAsClientIdAsync(Guid.NewGuid(), async cancellationToken =>
        {
            await processor.ProcessAsync(cancellationToken);
        });
    }
}

public sealed class Processor
{
    private readonly IClientIdAccessor _clientIdAccessor;

    public Processor(IClientIdAccessor clientIdAccessor)
    {
        _clientIdAccessor = clientIdAccessor;
    }

    public Task ProcessAsync(CancellationToken cancellationToken)
    {
        var clientId = _clientIdAccessor.Current;

        Console.WriteLine($"Current client ID: {clientId}");

        return Task.CompletedTask;
    }
}
