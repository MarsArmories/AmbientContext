using System.Diagnostics.CodeAnalysis;
using AmbientContext.Abstractions;

namespace AmbientContext.Core;

/// <summary>
/// Provides storage and scoped execution for an ambient context.
/// </summary>
/// <typeparam name="TContext">The marker type that isolates this ambient context.</typeparam>
/// <typeparam name="TValue">The non-null value type carried by this ambient context.</typeparam>
public sealed class AmbientContextRuntime<TContext, TValue>
    where TContext : notnull
    where TValue : notnull
{
    private readonly AsyncLocal<Frame?> _current = new();
    private readonly string _contextName;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextRuntime{TContext, TValue}"/> class.
    /// </summary>
    /// <param name="contextName">The context name used in diagnostics and exception messages.</param>
    public AmbientContextRuntime(string contextName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);
        _contextName = contextName;
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
                throw new AmbientContextMissingException(_contextName, typeof(TValue));
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
    /// Executes a delegate inside an ambient context scope.
    /// </summary>
    public void Execute(TValue value, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using (Push(value))
        {
            action();
        }
    }

    /// <summary>
    /// Executes a delegate inside an ambient context scope and returns its result.
    /// </summary>
    public TResult Execute<TResult>(TValue value, Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using (Push(value))
        {
            return action();
        }
    }

    /// <summary>
    /// Executes an asynchronous delegate inside an ambient context scope.
    /// </summary>
    public async Task ExecuteAsync(
        TValue value,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        using (Push(value))
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes an asynchronous delegate inside an ambient context scope and returns its result.
    /// </summary>
    public async Task<TResult> ExecuteAsync<TResult>(
        TValue value,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        using (Push(value))
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
    }

    private PopToken Push(TValue value)
    {
        if (value is null)
        {
            throw new AmbientContextNullValueException(_contextName, typeof(TValue));
        }

        var previous = _current.Value;
        var next = new Frame(value);
        _current.Value = next;

        return new PopToken(this, next, previous);
    }

    private void Pop(Frame expected, Frame? previous)
    {
        if (!ReferenceEquals(_current.Value, expected))
        {
            throw new AmbientContextStackCorruptionException(_contextName, typeof(TValue));
        }

        _current.Value = previous;
    }

    private sealed class Frame
    {
        public Frame(TValue value)
        {
            Value = value;
        }

        public TValue Value { get; }
    }

    private sealed class PopToken : IDisposable
    {
        private AmbientContextRuntime<TContext, TValue>? _runtime;
        private readonly Frame _expected;
        private readonly Frame? _previous;

        public PopToken(AmbientContextRuntime<TContext, TValue> runtime, Frame expected, Frame? previous)
        {
            _runtime = runtime;
            _expected = expected;
            _previous = previous;
        }

        public void Dispose()
        {
            var runtime = Interlocked.Exchange(ref _runtime, null);
            runtime?.Pop(_expected, _previous);
        }
    }
}
