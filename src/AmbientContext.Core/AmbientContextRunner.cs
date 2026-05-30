namespace AmbientContext.Core;

/// <summary>
/// Runs delegates inside a scoped ambient context value.
/// </summary>
/// <typeparam name="TContext">The marker type that isolates this ambient context.</typeparam>
/// <typeparam name="TValue">The non-null value type carried by this ambient context.</typeparam>
public sealed class AmbientContextRunner<TContext, TValue>
    where TContext : notnull
    where TValue : notnull
{
    private readonly AmbientContextState<TContext, TValue> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextRunner{TContext, TValue}"/> class.
    /// </summary>
    /// <param name="state">The state backing this runner.</param>
    public AmbientContextRunner(AmbientContextState<TContext, TValue> state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary>
    /// Executes an asynchronous delegate inside an ambient context scope.
    /// </summary>
    /// <param name="value">The non-null ambient value.</param>
    /// <param name="action">The delegate to execute.</param>
    /// <param name="cancellationToken">A cancellation token passed to the delegate.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(
        TValue value,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        using (_state.Push(value))
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes an asynchronous delegate inside an ambient context scope and returns its result.
    /// </summary>
    /// <typeparam name="TResult">The delegate result type.</typeparam>
    /// <param name="value">The non-null ambient value.</param>
    /// <param name="action">The delegate to execute.</param>
    /// <param name="cancellationToken">A cancellation token passed to the delegate.</param>
    /// <returns>A task that contains the delegate result.</returns>
    public async Task<TResult> ExecuteAsync<TResult>(
        TValue value,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        using (_state.Push(value))
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes a value-task delegate inside an ambient context scope.
    /// </summary>
    /// <param name="value">The non-null ambient value.</param>
    /// <param name="action">The delegate to execute.</param>
    /// <param name="cancellationToken">A cancellation token passed to the delegate.</param>
    /// <returns>A value task that represents the asynchronous operation.</returns>
    public async ValueTask ExecuteAsync(
        TValue value,
        Func<CancellationToken, ValueTask> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        using (_state.Push(value))
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes a value-task delegate inside an ambient context scope and returns its result.
    /// </summary>
    /// <typeparam name="TResult">The delegate result type.</typeparam>
    /// <param name="value">The non-null ambient value.</param>
    /// <param name="action">The delegate to execute.</param>
    /// <param name="cancellationToken">A cancellation token passed to the delegate.</param>
    /// <returns>A value task that contains the delegate result.</returns>
    public async ValueTask<TResult> ExecuteAsync<TResult>(
        TValue value,
        Func<CancellationToken, ValueTask<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        using (_state.Push(value))
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
    }
}
