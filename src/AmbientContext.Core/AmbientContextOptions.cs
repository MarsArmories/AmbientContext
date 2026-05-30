namespace AmbientContext.Core;

/// <summary>
/// Provides options for an ambient context state instance.
/// </summary>
/// <typeparam name="TContext">The marker type that isolates this ambient context.</typeparam>
/// <typeparam name="TValue">The non-null value type carried by this ambient context.</typeparam>
public sealed class AmbientContextOptions<TContext, TValue>
    where TContext : notnull
    where TValue : notnull
{
    /// <summary>
    /// Gets the ambient context name used in diagnostics and exception messages.
    /// </summary>
    public required string ContextName { get; init; }
}
