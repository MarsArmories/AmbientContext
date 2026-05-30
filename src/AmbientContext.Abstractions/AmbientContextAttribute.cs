namespace AmbientContext.Abstractions;

/// <summary>
/// Declares a named ambient context to be generated for the consuming assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AmbientContextAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextAttribute"/> class.
    /// </summary>
    /// <param name="valueType">The non-null value type carried by the ambient context.</param>
    /// <param name="name">The C# identifier base used for generated context types and methods.</param>
    public AmbientContextAttribute(Type valueType, string name)
    {
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Ambient context name cannot be empty.", nameof(name));
        }

        Name = name;
    }

    /// <summary>
    /// Gets the non-null value type carried by the ambient context.
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// Gets the C# identifier base used for generated context types and methods.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the namespace used for generated types, or <see langword="null"/> to use the consumer's root namespace.
    /// </summary>
    public string? Namespace { get; init; }
}
