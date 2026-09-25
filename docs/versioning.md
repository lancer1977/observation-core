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

The internal feed is:

`https://nuget.polyhydragames.com/v3/index.json`

It supports anonymous package reads. A consumer that wants to restrict this
package to that feed can use NuGet package source mapping:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="polyhydra" value="https://nuget.polyhydragames.com/v3/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="polyhydra">
      <package pattern="Observation.Core" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="Microsoft.*" />
      <package pattern="xunit" />
      <package pattern="xunit.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

The mapping is an example policy; add the consumer's other dependency
patterns to `nuget.org` as needed, while leaving `Observation.Core` mapped
only to `polyhydra`. Never overwrite a published package version.
If a package must change, publish a new SemVer version and update consumers.
