namespace AmbientContext.Abstractions;

/// <summary>
/// Represents the base type for AmbientContext exceptions.
/// </summary>
public abstract class AmbientContextException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    protected AmbientContextException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    protected AmbientContextException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
