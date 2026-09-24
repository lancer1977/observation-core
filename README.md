# Observation Core

Observation Core is a shared, domain-neutral evidence layer for representing observations, confidence, provenance, disagreement, fusion, and bounded reuse.

## Domain-neutral boundary

Observation Core contains no game, TV, or media concepts; no OCR, VLM, OpenCV, FFmpeg, or device dependencies; and no action authorization, stream switching, or consumer policy. Observation produces knowledge. Consumers decide actions.

## Build and test

```sh
dotnet restore ObservationCore.slnx
dotnet build ObservationCore.slnx -c Release
dotnet test ObservationCore.slnx -c Release
dotnet pack src/Observation.Core/Observation.Core.csproj -c Release --no-build -o ./artifacts
```
