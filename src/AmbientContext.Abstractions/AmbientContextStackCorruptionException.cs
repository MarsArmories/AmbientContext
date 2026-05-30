namespace AmbientContext.Abstractions;

/// <summary>
/// Represents an error that occurs when ambient context scopes are disposed out of order.
/// </summary>
public sealed class AmbientContextStackCorruptionException : AmbientContextException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextStackCorruptionException"/> class.
    /// </summary>
    /// <param name="contextName">The ambient context name.</param>
    /// <param name="valueType">The ambient context value type.</param>
    public AmbientContextStackCorruptionException(string contextName, Type valueType)
        : base(CreateMessage(contextName, valueType))
    {
        ContextName = contextName;
        ValueType = valueType;
    }

    /// <summary>
    /// Gets the ambient context name.
    /// </summary>
    public string ContextName { get; }

    /// <summary>
    /// Gets the ambient context value type.
    /// </summary>
    public Type ValueType { get; }

    private static string CreateMessage(string contextName, Type valueType)
    {
        return $"Ambient context stack corruption was detected for context '{contextName}' " +
            $"with value type '{valueType.FullName}'. " +
            "Ambient context scopes must be disposed in last-in-first-out order.";
    }
}
