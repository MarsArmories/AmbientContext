using System.Diagnostics.CodeAnalysis;
using AmbientContext.Abstractions;

namespace AmbientContext.Core;

/// <summary>
/// Stores scoped ambient context values using <see cref="AsyncLocal{T}"/>.
/// </summary>
/// <typeparam name="TContext">The marker type that isolates this ambient context.</typeparam>
/// <typeparam name="TValue">The non-null value type carried by this ambient context.</typeparam>
public sealed class AmbientContextState<TContext, TValue>
    where TContext : notnull
    where TValue : notnull
{
    private readonly AsyncLocal<Frame?> _current = new();
    private readonly AmbientContextOptions<TContext, TValue> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextState{TContext, TValue}"/> class.
    /// </summary>
    /// <param name="options">The options for this ambient context.</param>
    public AmbientContextState(AmbientContextOptions<TContext, TValue> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets a value indicating whether a current ambient value exists.
    /// </summary>
    public bool HasCurrent => _current.Value is not null;

    /// <summary>
    /// Gets the current ambient value.
    /// </summary>
    /// <exception cref="AmbientContextMissingException">No ambient value is available.</exception>
    public TValue Current
    {
        get
        {
            if (_current.Value is not { } frame)
            {
                throw new AmbientContextMissingException(_options.ContextName, typeof(TValue));
            }

            return frame.Value;
        }
    }

    /// <summary>
    /// Gets the current ambient value, or <see langword="default"/> when no value is available.
    /// </summary>
    public TValue? CurrentOrDefault => _current.Value is { } frame ? frame.Value : default;

    /// <summary>
    /// Attempts to get the current ambient value.
    /// </summary>
    /// <param name="value">The current ambient value when one is available.</param>
    /// <returns><see langword="true"/> when a value is available; otherwise, <see langword="false"/>.</returns>
    public bool TryGetCurrent([NotNullWhen(true)] out TValue? value)
    {
        if (_current.Value is { } frame)
        {
            value = frame.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Pushes a value into the ambient context until the returned token is disposed.
    /// </summary>
    /// <param name="value">The non-null value to push.</param>
    /// <returns>A token that restores the previous value when disposed.</returns>
    /// <exception cref="AmbientContextNullValueException">The value is <see langword="null"/>.</exception>
    public IDisposable Push(TValue value)
    {
        if (value is null)
        {
            throw new AmbientContextNullValueException(_options.ContextName, typeof(TValue));
        }

        var previous = _current.Value;
        var next = new Frame(value, previous);

        _current.Value = next;

        return new PopToken(this, next, previous);
    }

    private void Pop(Frame expected, Frame? previous)
    {
        if (!ReferenceEquals(_current.Value, expected))
        {
            throw new AmbientContextStackCorruptionException(_options.ContextName, typeof(TValue));
        }

        _current.Value = previous;
    }

    private sealed class Frame
    {
        public Frame(TValue value, Frame? previous)
        {
            Value = value;
            Previous = previous;
        }

        public TValue Value { get; }

        public Frame? Previous { get; }
    }

    private sealed class PopToken : IDisposable
    {
        private AmbientContextState<TContext, TValue>? _state;
        private readonly Frame _expected;
        private readonly Frame? _previous;

        public PopToken(AmbientContextState<TContext, TValue> state, Frame expected, Frame? previous)
        {
            _state = state;
            _expected = expected;
            _previous = previous;
        }

        public void Dispose()
        {
            var state = Interlocked.Exchange(ref _state, null);

            if (state is null)
            {
                return;
            }

            state.Pop(_expected, _previous);
        }
    }
}
