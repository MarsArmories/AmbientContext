namespace AmbientContext.Abstractions;

/// <summary>
/// Represents an error that occurs when a null ambient context value is pushed.
/// </summary>
public sealed class AmbientContextNullValueException : AmbientContextException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextNullValueException"/> class.
    /// </summary>
    /// <param name="contextName">The ambient context name.</param>
    /// <param name="valueType">The ambient context value type.</param>
    public AmbientContextNullValueException(string contextName, Type valueType)
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
        return $"Ambient context '{contextName}' does not allow null values. " +
            $"The value type is '{valueType.FullName}'.";
    }
}
