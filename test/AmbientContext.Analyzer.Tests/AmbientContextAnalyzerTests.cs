using System.Collections.Immutable;
using AmbientContext.Analyzers;
using AmbientContext.Core;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmbientContext.Analyzer.Tests;

[TestClass]
public sealed class AmbientContextAnalyzerTests
{
    [TestMethod]
    public async Task Warns_for_TaskRun_inside_ExecuteAsAsync()
    {
        var diagnostics = await RunAnalyzerAsync("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Worker
            {
                public async Task Run(ClientIdContext context)
                {
                    await context.ExecuteAsClientIdAsync(Guid.NewGuid(), async ct =>
                    {
                        _ = Task.Run(() => Task.CompletedTask);
                        await Task.CompletedTask;
                    });
                }
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("AC0002");
    }

    [TestMethod]
    public async Task Does_not_warn_for_TaskRun_inside_ExecutionContextSuppressFlow()
    {
        var diagnostics = await RunAnalyzerAsync("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Worker
            {
                public async Task Run(ClientIdContext context)
                {
                    await context.ExecuteAsClientIdAsync(Guid.NewGuid(), async ct =>
                    {
                        using (ExecutionContext.SuppressFlow())
                        {
                            _ = Task.Run(() => Task.CompletedTask);
                        }

                        await Task.CompletedTask;
                    });
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Does_not_warn_for_TaskRun_inside_AmbientContextFlowSuppressFor()
    {
        var diagnostics = await RunAnalyzerAsync("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using AmbientContext.Core;

            public sealed class Worker
            {
                public async Task Run(ClientIdContext context)
                {
                    await context.ExecuteAsClientIdAsync(Guid.NewGuid(), async ct =>
                    {
                        AmbientContextFlow.SuppressFor(() =>
                        {
                            _ = Task.Run(() => Task.CompletedTask);
                        });

                        await Task.CompletedTask;
                    });
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Warns_for_ThreadPoolQueueUserWorkItem()
    {
        var diagnostics = await RunAnalyzerAsync("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Worker
            {
                public async Task Run(ClientIdContext context)
                {
                    await context.ExecuteAsClientIdAsync(Guid.NewGuid(), async ct =>
                    {
                        ThreadPool.QueueUserWorkItem(_ => { });
                        await Task.CompletedTask;
                    });
                }
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("AC0003");
    }

    [TestMethod]
    public async Task Warns_for_TaskFactoryStartNew()
    {
        var diagnostics = await RunAnalyzerAsync("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Worker
            {
                public async Task Run(ClientIdContext context)
                {
                    await context.ExecuteAsClientIdAsync(Guid.NewGuid(), async ct =>
                    {
                        _ = Task.Factory.StartNew(() => { });
                        await Task.CompletedTask;
                    });
                }
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("AC0004");
    }

    [TestMethod]
    public async Task Does_not_warn_outside_ExecuteAsAsync()
    {
        var diagnostics = await RunAnalyzerAsync("""
            using System.Threading.Tasks;

            public sealed class Worker
            {
                public void Run()
                {
                    _ = Task.Run(() => Task.CompletedTask);
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    private static async Task<IReadOnlyCollection<Diagnostic>> RunAnalyzerAsync(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "AmbientContext.AnalyzerTest",
            new[]
            {
                CSharpSyntaxTree.ParseText(source, parseOptions),
                CSharpSyntaxTree.ParseText(SupportSource, parseOptions)
            },
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationDiagnostics = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        compilationDiagnostics.Should().BeEmpty();

        var analyzerCompilation = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new AmbientContextAnalyzer()));

        return await analyzerCompilation.GetAnalyzerDiagnosticsAsync();
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        return trustedPlatformAssemblies.Concat(new[]
        {
            MetadataReference.CreateFromFile(typeof(AmbientContextFlow).Assembly.Location)
        });
    }

    private const string SupportSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        public sealed class ClientIdContext
        {
            public Task ExecuteAsClientIdAsync(
                Guid value,
                Func<CancellationToken, Task> action,
                CancellationToken cancellationToken = default)
            {
                return action(cancellationToken);
            }
        }
        """;
}
