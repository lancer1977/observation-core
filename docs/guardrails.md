# Architecture and dependency guardrails

Observation Core is the domain-neutral evidence layer. These tests keep that boundary executable in the normal test job.

## Dependency allowlists

- `Observation.Core.dll` may reference only `System.Collections`, `System.Linq`, `System.Runtime`, `System.Text.Json`, and `System.Threading`.
- `Observation.Core.csproj` has no allowed `PackageReference` or `ProjectReference`; the test allowlists are intentionally empty.
- The allowlists are exact. Adding a package, project, or assembly reference requires an intentional issue and an update to this document and the architecture test.

## Domain-term guardrail

Core source text and public type/member names may not contain `game`, `emulator`, `ocr`, `vlm`, `opencv`, `ffmpeg`, `hdhomerun`, `device`, or `stream` (case-insensitive). There are currently no legitimate-use exceptions. Consumer-specific vocabulary belongs in a consumer repository.

## Public API I/O guardrail

The public API may not reference `System.Net` or `System.IO` namespaces, including through public signatures. The core assembly also may not acquire direct assembly references to those namespaces.

These rules are enforced by `tests/Observation.Core.Tests/Architecture/ArchitectureGuardrailTests.cs`, so they run through the normal `dotnet test` job.
