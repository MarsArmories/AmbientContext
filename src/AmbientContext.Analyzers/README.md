# AmbientContext Analyzers

[![NuGet](https://img.shields.io/nuget/v/MarsArmories.AmbientContext.Analyzers.svg)](https://www.nuget.org/packages/MarsArmories.AmbientContext.Analyzers)

`MarsArmories.AmbientContext.Analyzers` provides optional Roslyn diagnostics for projects that use [MarsArmories.AmbientContext](https://www.nuget.org/packages/MarsArmories.AmbientContext).

The analyzers identify obvious fire-and-forget scheduling inside generated `ExecuteAs*Async` scopes. Work scheduled inside an ambient scope may capture the current `ExecutionContext` and continue observing ambient values after the scope has completed.

## Installation

Install the package in projects where you want the diagnostics to run:

```shell
dotnet package add MarsArmories.AmbientContext.Analyzers
```

For direct project references, keep the analyzer private to the consuming project:

```xml
<PackageReference Include="MarsArmories.AmbientContext.Analyzers"
                  Version="1.1.0"
                  PrivateAssets="all" />
```

## Diagnostics

The current analyzer rule inventory is maintained in:

* [Shipped analyzer rules](https://github.com/MarsArmories/AmbientContext/blob/main/src/AmbientContext.Analyzers/AnalyzerReleases.Shipped.md)
* [Unshipped analyzer rules](https://github.com/MarsArmories/AmbientContext/blob/main/src/AmbientContext.Analyzers/AnalyzerReleases.Unshipped.md)

## Resources

* [GitHub repository](https://github.com/MarsArmories/AmbientContext)
* [Runtime package](https://www.nuget.org/packages/MarsArmories.AmbientContext)
* [Security policy](https://github.com/MarsArmories/AmbientContext/blob/main/SECURITY.md)
* [MIT license](https://github.com/MarsArmories/AmbientContext/blob/main/LICENSE)

Please use [GitHub Issues](https://github.com/MarsArmories/AmbientContext/issues) for bugs and feature requests. Report security vulnerabilities privately as described in the [security policy](https://github.com/MarsArmories/AmbientContext/blob/main/SECURITY.md).
