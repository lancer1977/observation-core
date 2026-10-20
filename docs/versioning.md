# Package versioning policy

`Observation.Core` follows Semantic Versioning for its published package:

- A patch version is for backwards-compatible fixes and documentation.
- A minor version adds backwards-compatible public behavior.
- A major version is required for a breaking change.

While the package is `0.x`, it is preview software: minor-version changes may
include breaking changes before `1.0`, but they should still be called out in
release notes and compatibility fixtures.

Breaking changes include:

- removing, renaming, or changing the signatures or validation of public API;
- changing fusion or cache semantics, including statuses, grouping, expiry,
  invalidation, ordering, or provenance behavior; or
- changing behavior covered by compatibility fixtures, even if the C# API
  still compiles.

The compatibility fixtures under [`docs/compatibility/`](compatibility/) and
their test fixtures make selected consumer-visible behavior explicit. Update
them only when the intended compatibility contract changes; otherwise they
are a guard against accidental semantic changes.

## Package feed

The planned public preview feed is nuget.org. Update consumers only after the
requested package version is published and independently verified:

`https://api.nuget.org/v3/index.json`

Install the package with:

```sh
dotnet add package Observation.Core --version 0.1.0-preview.3
```

Consumers that use package source mapping should map `Observation.Core` to
nuget.org. The repository's internal BaGet feed remains available for existing
private consumers during the transition:

`https://nuget.polyhydragames.com/v3/index.json`

The mapping for a consumer that allows public package reads can be:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="Observation.Core" />
      <package pattern="Microsoft.*" />
      <package pattern="xunit" />
      <package pattern="xunit.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

The mapping is an example policy; add the consumer's other dependency
patterns to `nuget.org` as needed. Existing consumers can continue using the
internal feed until their feed and version transition is scheduled. Never
overwrite a published package version.
If a package must change, publish a new SemVer version and update consumers.

## Publication workflow

The `Publish NuGet.org` workflow is manual and builds a dry-run package by
default. Its publish job runs only from `main`, requires the dispatch version
to match the project version, and uses the `nuget-org` environment. Before a
publication is authorized, configure that environment to allow deployments
from `main` only and store `NUGET_ORG_API_KEY` as its environment secret.
