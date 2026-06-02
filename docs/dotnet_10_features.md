# What's New in .NET 10

.NET 10 is a Long-Term Support (LTS) release focused on runtime performance, developer productivity, cloud-native development, and AI integration.

## Language Improvements

### C# 14

Key C# 14 additions include:

- **Extension members:** Static and instance extension members can be grouped in extension blocks.
- **Field-backed properties:** The `field` contextual keyword gives property accessors access to the compiler-generated backing field.
- **Null-conditional assignment:** Assign through `?.` when the receiver is not null.
- **`nameof` improvements:** `nameof` supports unbound generic types such as `List<>`.
- **Span conversions:** Improved implicit conversions for `Span<T>` and `ReadOnlySpan<T>`.
- **Lambda parameter modifiers:** `ref`, `in`, and `out` can be used on simple lambda parameters.
- **Partial members:** Support expands to partial constructors and events.
- **User-defined operators:** Compound assignment and increment/decrement operators are supported.

### F# 10

F# 10 focuses on language, compiler, and library improvements, including scoped warning controls, improved property accessors, and performance-oriented updates in the compiler and standard library.

## Runtime and Performance

.NET 10 includes runtime improvements across:

- **JIT optimization:** Better inlining, loop inversion, devirtualization, and code generation.
- **Hardware acceleration:** AVX10.2 support and continued Arm64 optimization.
- **Stack allocation:** More opportunities for short-lived objects and arrays to avoid heap allocation.
- **NativeAOT:** Continued improvements to application size and startup time.
- **Garbage collection:** Ongoing tuning to reduce pause time and improve throughput.

## Web and Cloud-Native Development

ASP.NET Core 10 includes updates across:

- **Security and identity:** Passkey/FIDO2 support for authentication scenarios.
- **Minimal APIs:** Validation and JSON Patch improvements.
- **OpenAPI:** Enhanced OpenAPI support, including OpenAPI 3.1.
- **Blazor:** State persistence, loading, diagnostics, and form validation improvements.

.NET 10 also continues expanding support for AI and cloud-native applications through `Microsoft.Extensions.AI`, Aspire, and Model Context Protocol (MCP)-related tooling.

## SDK and Tooling

Notable SDK updates include:

- Standardized `dotnet` CLI command ordering.
- Native tab-completion scripts for popular shells.
- Better `Microsoft.Testing.Platform` support in `dotnet test`.
- Improved support for file-based apps and one-shot tool execution.
- Continued NuGet security and dependency auditing improvements.

## Project Note

This repo currently targets .NET 8 in the main tutorial services. Treat these .NET 10 notes as upgrade research rather than current project requirements.
