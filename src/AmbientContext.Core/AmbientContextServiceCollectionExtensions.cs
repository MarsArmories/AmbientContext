using Microsoft.Extensions.DependencyInjection;

namespace AmbientContext.Core;

/// <summary>
/// Provides dependency injection registration helpers for ambient contexts.
/// </summary>
public static class AmbientContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core services for an ambient context.
    /// </summary>
    /// <typeparam name="TContext">The marker type that isolates this ambient context.</typeparam>
    /// <typeparam name="TValue">The non-null value type carried by this ambient context.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="contextName">The context name used in diagnostics and exception messages.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAmbientContext<TContext, TValue>(
        this IServiceCollection services,
        string contextName)
        where TContext : notnull
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);

        services.AddSingleton(new AmbientContextOptions<TContext, TValue>
        {
            ContextName = contextName
        });

        services.AddSingleton<AmbientContextState<TContext, TValue>>();
        services.AddSingleton<AmbientContextAccessor<TContext, TValue>>();
        services.AddSingleton<AmbientContextRunner<TContext, TValue>>();

        return services;
    }
}
