namespace AmbientContext.Abstractions;

/// <summary>
/// Represents an error that occurs when an ambient context is read outside its active scope.
/// </summary>
public sealed class AmbientContextMissingException : AmbientContextException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextMissingException"/> class.
    /// </summary>
    /// <param name="contextName">The ambient context name.</param>
    /// <param name="valueType">The ambient context value type.</param>
    public AmbientContextMissingException(string contextName, Type valueType)
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
        return $"No ambient value is currently available for context '{contextName}' " +
            $"with value type '{valueType.FullName}'. " +
            $"Make sure the code is running inside the matching ExecuteAs{contextName}Async scope.";
    }
}
