using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AmbientContext.Analyzers;

/// <summary>
/// Reports obvious fire-and-forget scheduling inside generated ambient context scopes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AmbientContextAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            AmbientContextAnalyzerDiagnosticDescriptors.FireAndForget,
            AmbientContextAnalyzerDiagnosticDescriptors.TaskRun,
            AmbientContextAnalyzerDiagnosticDescriptors.ThreadPoolQueue,
            AmbientContextAnalyzerDiagnosticDescriptors.TaskFactoryStartNew);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsExecuteAsInvocation(invocation))
        {
            return;
        }

        foreach (var lambda in invocation.ArgumentList.Arguments.Select(argument => argument.Expression).OfType<LambdaExpressionSyntax>())
        {
            foreach (var childInvocation in lambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (IsInsideFlowSuppression(childInvocation, lambda))
                {
                    continue;
                }

                var descriptor = GetDescriptor(childInvocation, context.SemanticModel, context.CancellationToken);

                if (descriptor is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(descriptor, childInvocation.GetLocation()));
                }
            }
        }
    }

    private static bool IsExecuteAsInvocation(InvocationExpressionSyntax invocation)
    {
        return GetInvokedMemberName(invocation) is { } name &&
            name.StartsWith("ExecuteAs", StringComparison.Ordinal) &&
            name.EndsWith("Async", StringComparison.Ordinal);
    }

    private static DiagnosticDescriptor? GetDescriptor(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var symbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;

        if (symbol is null)
        {
            return null;
        }

        var containingType = symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (symbol.Name == "Run" && containingType == "global::System.Threading.Tasks.Task")
        {
            return AmbientContextAnalyzerDiagnosticDescriptors.TaskRun;
        }

        if (symbol.Name == "StartNew" && containingType == "global::System.Threading.Tasks.TaskFactory")
        {
            return AmbientContextAnalyzerDiagnosticDescriptors.TaskFactoryStartNew;
        }

        if (symbol.Name == "QueueUserWorkItem" && containingType == "global::System.Threading.ThreadPool")
        {
            return AmbientContextAnalyzerDiagnosticDescriptors.ThreadPoolQueue;
        }

        return null;
    }

    private static bool IsInsideFlowSuppression(InvocationExpressionSyntax invocation, LambdaExpressionSyntax ambientLambda)
    {
        foreach (var ancestor in invocation.Ancestors())
        {
            if (ReferenceEquals(ancestor, ambientLambda))
            {
                return false;
            }

            if (ancestor is UsingStatementSyntax usingStatement &&
                IsSuppressFlowInvocation(usingStatement.Expression))
            {
                return true;
            }

            if (ancestor is InvocationExpressionSyntax suppressionInvocation &&
                IsAmbientContextFlowSuppressFor(suppressionInvocation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSuppressFlowInvocation(ExpressionSyntax? expression)
    {
        return expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText == "SuppressFlow" &&
            memberAccess.Expression.ToString().EndsWith("ExecutionContext", StringComparison.Ordinal);
    }

    private static bool IsAmbientContextFlowSuppressFor(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText == "SuppressFor" &&
            memberAccess.Expression.ToString().EndsWith("AmbientContextFlow", StringComparison.Ordinal);
    }

    private static string? GetInvokedMemberName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };
    }
}
