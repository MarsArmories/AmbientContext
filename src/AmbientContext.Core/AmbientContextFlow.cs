namespace AmbientContext.Core;

/// <summary>
/// Provides helpers for suppressing <see cref="ExecutionContext"/> flow around work scheduling.
/// </summary>
public static class AmbientContextFlow
{
    /// <summary>
    /// Suppresses <see cref="ExecutionContext"/> flow while the supplied action schedules work.
    /// </summary>
    /// <param name="action">The scheduling action to execute while flow is suppressed.</param>
    public static void SuppressFor(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using (ExecutionContext.SuppressFlow())
        {
            action();
        }
    }

    /// <summary>
    /// Suppresses <see cref="ExecutionContext"/> flow while the supplied function schedules work.
    /// </summary>
    /// <typeparam name="TResult">The function result type.</typeparam>
    /// <param name="action">The scheduling function to execute while flow is suppressed.</param>
    /// <returns>The result returned by <paramref name="action"/>.</returns>
    public static TResult SuppressFor<TResult>(Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using (ExecutionContext.SuppressFlow())
        {
            return action();
        }
    }
}
