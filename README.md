# Observation Core

Observation Core is a shared, domain-neutral evidence layer for representing observations, confidence, provenance, disagreement, fusion, and bounded reuse.

## Domain-neutral boundary

Observation Core contains no game, TV, or media concepts; no OCR, VLM, OpenCV, FFmpeg, or device dependencies; and no action authorization, stream switching, or consumer policy. Observation produces knowledge. Consumers decide actions.

## Contracts

`ObservationScope` models an immutable ordered sequence of non-blank scope segments with ordinal equality, comparison, prefix checks, and append support. `Observation<T>` carries a validated value, provenance, timestamp, and finite confidence in the inclusive range `[0, 1]`.

## Build and test

```sh
dotnet restore ObservationCore.slnx
dotnet build ObservationCore.slnx -c Release
dotnet test ObservationCore.slnx -c Release
dotnet pack src/Observation.Core/Observation.Core.csproj -c Release --no-build -o ./artifacts
```
