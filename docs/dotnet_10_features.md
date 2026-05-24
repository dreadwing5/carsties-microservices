# What's New in .NET 10

.NET 10 is a Long-Term Support (LTS) release focused on performance optimizations, developer productivity, and expanded AI integration. 

Here are the key highlights and features introduced in .NET 10:

## 1. Language Improvements (C# 14 & F# 10)
* **C# 14**:
  * **Field-backed properties**: Allows using the `field` keyword to simplify property declarations without needing explicit backing fields.
  * **File-based apps**: Run C# code directly from a single `Program.cs` file without a project or solution file. Useful for scripts and small utilities.
  * **Other additions**: Null-conditional assignment, partial constructors, ref struct interface support, and collection expression extensions.
* **F# 10**: Focuses on clarity and performance with scoped warning controls, improved property accessors, and struct-based optional parameters.

## 2. Runtime and Performance
.NET 10 is considered one of the fastest releases to date:
* **JIT Compiler Enhancements**: Improvements in method inlining, loop inversion, and more aggressive devirtualization.
* **Hardware Acceleration**: Support for AVX10.2 (Intel) and Arm64 SVE (Scalable Vector Extension), alongside improved write-barrier handling reducing garbage collection (GC) pauses by 8–20%.
* **Stack Allocation**: The JIT can now allocate small arrays (including reference type arrays) on the stack when they do not outlive their creation context, reducing heap allocation overhead.
* **NativeAOT**: Continued improvements to reduce application size and startup times.

## 3. Web and Cloud-Native Development
* **ASP.NET Core 10**:
  * **Security & Identity**: Introduced passkey (FIDO2) support for authentication.
  * **Minimal APIs**: Added support for validation and JSON Patch.
  * **OpenAPI**: Enhanced support for OpenAPI 3.1.
  * **Blazor**: Improvements include state persistence, optimized loading, and enhanced form validation.
* **AI Integration**: Expanded support for building AI-powered applications through `Microsoft.Extensions.AI`, the Microsoft Agent Framework, and first-class Model Context Protocol (MCP) support.
* **Aspire**: Updated to support better orchestration for front ends, APIs, and containers, including improved dashboard and deployment workflows.

## 4. SDK and Tooling
* **CLI Enhancements**: Standardized CLI command order and native tab-completion scripts.
* **Testing**: Enhanced support for `Microsoft.Testing.Platform` (MTP) in `dotnet test`.
* **Containerization**: Console applications can now natively create container images, with new options to explicitly set image formats.
* **NuGet**: Continued focus on security with improvements to dependency auditing.
