using System.Diagnostics.CodeAnalysis;

namespace AmbientContext.Core;

/// <summary>
/// Reads the current value for an ambient context.
/// </summary>
/// <typeparam name="TContext">The marker type that isolates this ambient context.</typeparam>
/// <typeparam name="TValue">The non-null value type carried by this ambient context.</typeparam>
public sealed class AmbientContextAccessor<TContext, TValue>
    where TContext : notnull
    where TValue : notnull
{
    private readonly AmbientContextState<TContext, TValue> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextAccessor{TContext, TValue}"/> class.
    /// </summary>
    /// <param name="state">The state backing this accessor.</param>
    public AmbientContextAccessor(AmbientContextState<TContext, TValue> state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary>
    /// Gets a value indicating whether a current ambient value exists.
    /// </summary>
    public bool HasCurrent => _state.HasCurrent;

    /// <summary>
    /// Gets the current ambient value.
    /// </summary>
    /// <exception cref="AmbientContext.Abstractions.AmbientContextMissingException">No ambient value is available.</exception>
    public TValue Current => _state.Current;

    /// <summary>
    /// Gets the current ambient value, or <see langword="default"/> when no value is available.
    /// </summary>
    public TValue? CurrentOrDefault => _state.CurrentOrDefault;

    /// <summary>
    /// Attempts to get the current ambient value.
    /// </summary>
    /// <param name="value">The current ambient value when one is available.</param>
    /// <returns><see langword="true"/> when a value is available; otherwise, <see langword="false"/>.</returns>
    public bool TryGetCurrent([NotNullWhen(true)] out TValue? value)
    {
        return _state.TryGetCurrent(out value);
    }
}
