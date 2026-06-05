using System.Reflection;
using AmbientContext.Abstractions;
using AmbientContext.Core;
using AwesomeAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbientContext.Tests;

[TestClass]
public sealed class PublicApiSurfaceTests
{
    [TestMethod]
    public void Abstractions_public_api_matches_approved_surface()
    {
        AssertApproved(typeof(AmbientContextAttribute).Assembly);
    }

    [TestMethod]
    public void Core_public_api_matches_approved_surface()
    {
        AssertApproved(typeof(AmbientContextRuntime<,>).Assembly);
    }

    private static void AssertApproved(Assembly assembly)
    {
        var approvedPath = Path.Combine(
            AppContext.BaseDirectory,
            "ApprovedApi",
            $"{assembly.GetName().Name}.txt");
        var approved = File.ReadAllLines(approvedPath)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actual = GetPublicApi(assembly).Order(StringComparer.Ordinal).ToArray();

        actual.Should().Equal(approved);
    }

    private static IEnumerable<string> GetPublicApi(Assembly assembly)
    {
        foreach (var type in assembly.GetExportedTypes().OrderBy(static type => type.FullName))
        {
            yield return $"T:{GetTypeKind(type)} {FormatType(type)}";

            const BindingFlags flags =
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly;

            foreach (var constructor in type.GetConstructors(flags).Where(IsVisible))
            {
                yield return $"C:{FormatType(type)}({FormatParameters(constructor.GetParameters())})";
            }

            foreach (var method in type.GetMethods(flags)
                .Where(static method => !method.IsSpecialName)
                .Where(IsVisible))
            {
                var genericArguments = method.IsGenericMethodDefinition
                    ? $"<{string.Join(",", method.GetGenericArguments().Select(FormatType))}>"
                    : string.Empty;
                yield return $"M:{FormatType(type)}.{method.Name}{genericArguments}" +
                    $"({FormatParameters(method.GetParameters())}):{FormatType(method.ReturnType)}";
            }

            foreach (var property in type.GetProperties(flags).Where(IsVisible))
            {
                yield return $"P:{FormatType(type)}.{property.Name}:{FormatType(property.PropertyType)}";
            }
        }
    }

    private static bool IsVisible(MethodBase method)
    {
        return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
    }

    private static bool IsVisible(PropertyInfo property)
    {
        return (property.GetMethod is not null && IsVisible(property.GetMethod)) ||
            (property.SetMethod is not null && IsVisible(property.SetMethod));
    }

    private static string GetTypeKind(Type type)
    {
        if (type.IsInterface)
        {
            return "interface";
        }

        if (type.IsAbstract && type.IsSealed)
        {
            return "static class";
        }

        if (type.IsAbstract)
        {
            return "abstract class";
        }

        return type.IsSealed ? "sealed class" : "class";
    }

    private static string FormatParameters(IEnumerable<ParameterInfo> parameters)
    {
        return string.Join(",", parameters.Select(static parameter => FormatType(parameter.ParameterType)));
    }

    private static string FormatType(Type type)
    {
        if (type.IsByRef)
        {
            return $"{FormatType(type.GetElementType()!)}&";
        }

        if (type.IsArray)
        {
            return $"{FormatType(type.GetElementType()!)}[]";
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (!type.IsGenericType)
        {
            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        var definitionName = type.GetGenericTypeDefinition().FullName!;
        var tickIndex = definitionName.IndexOf('`');
        var name = tickIndex >= 0 ? definitionName[..tickIndex] : definitionName;
        var arguments = string.Join(",", type.GetGenericArguments().Select(FormatType));
        return $"{name.Replace('+', '.')}<{arguments}>";
    }
}
