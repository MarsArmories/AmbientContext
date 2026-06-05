namespace AmbientContext.Abstractions;

/// <summary>
/// Configures the optional aggregate registration method generated for an assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AmbientContextRegistrationAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientContextRegistrationAttribute"/> class.
    /// </summary>
    /// <param name="methodName">The aggregate registration extension method name.</param>
    public AmbientContextRegistrationAttribute(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            throw new ArgumentException("Registration method name cannot be empty.", nameof(methodName));
        }

        MethodName = methodName;
    }

    /// <summary>
    /// Gets the aggregate registration extension method name.
    /// </summary>
    public string MethodName { get; }
}
