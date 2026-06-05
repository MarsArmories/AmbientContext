# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

## [2.0.0] - 2026-06-05

### Added

* Added optional `AmbientContextRegistrationAttribute` for collision-resistant, assembly-specific aggregate registration methods.
* Added generated `I{Name}Context` interfaces and synchronous `ExecuteAs{Name}` overloads.
* Added generator diagnostics for invalid namespaces, open generic values, duplicate or invalid aggregate registration declarations, and generated name collisions.
* Added approved public API snapshots and NuGet package validation.
* Added integration guidance and buildable ASP.NET Core and message-processing samples.

### Changed

* Consolidated the public core runtime into `AmbientContextRuntime<TContext, TValue>`.
* Generated markers, accessors, context implementations, constructors, and registration helpers are now internal.
* Generated dependency injection registration is idempotent.
* Analyzer matching now identifies generated AmbientContext methods semantically.

### Removed

* Removed public `ValueTask` execution overloads in favor of unambiguous Task-based delegates.
* Removed the collision-prone parameterless `AddAmbientContext()` aggregate methods.
* Removed the unused `MAAC0001` analyzer rule.
* Removed the previous public accessor, runner, state, and options infrastructure types.

### Fixed

* Updated Forgejo package publishing to use a dedicated package-scoped personal access token and support retrying an existing release tag manually.
* Enabled automatic NuGet.org trusted publishing for release tag pushes while retaining safe manual retries for existing tags.
* Publishing workflows now create idempotent GitHub and Forgejo releases from the matching changelog section after packages are published.

## [1.1.0] - 2026-06-05

### Added

* Added source-generated `AddAmbientContext()` helpers for `IServiceCollection` and `IHostBuilder` that register every ambient context declared in the consuming assembly.
* Added integration coverage for aggregate dependency injection and generic host registration.

### Changed

* Generated registration implementation helpers are now file-local types to avoid collisions in consuming projects.
* Updated the sample and primary documentation to use aggregate registration by default.

## [1.0.0] - 2026-05-31

### Added

* Strongly typed ambient context wrappers over `AsyncLocal<T>`.
* Source-generated named context APIs, accessors, and dependency injection registration helpers.
* Optional Roslyn diagnostics for fire-and-forget work that may capture ambient values.
* Support for .NET 8 and .NET 10.

## [0.1.0-alpha.3] - 2026-05-31

### Changed

* Added NuGet installation guidance and repository resource links.
* Added a dedicated README for the optional analyzer package.

## [0.1.0-alpha.2] - 2026-05-31

### Changed

* Refined package metadata and repository configuration for public preview publication.

## [0.1.0-alpha.1] - 2026-05-31

### Added

* Initial preview packages.

[Unreleased]: https://github.com/MarsArmories/AmbientContext/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/MarsArmories/AmbientContext/compare/v1.1.0...v2.0.0
[1.1.0]: https://github.com/MarsArmories/AmbientContext/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/MarsArmories/AmbientContext/compare/v0.1.0-alpha.3...v1.0.0
[0.1.0-alpha.3]: https://github.com/MarsArmories/AmbientContext/compare/v0.1.0-alpha.2...v0.1.0-alpha.3
[0.1.0-alpha.2]: https://github.com/MarsArmories/AmbientContext/compare/v0.1.0-alpha.1...v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/MarsArmories/AmbientContext/releases/tag/v0.1.0-alpha.1
