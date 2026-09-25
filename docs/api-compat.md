# Package API compatibility

The `Observation.Core` package enables the .NET SDK package-validation targets
and compares each packed package with the published baseline
`0.1.0-preview.1`:

```xml
<EnablePackageValidation>true</EnablePackageValidation>
<PackageValidationBaselineVersion>0.1.0-preview.1</PackageValidationBaselineVersion>
```

The baseline is downloaded anonymously from the Polyhydra internal NuGet feed.
`nuget.config` maps only the `Observation.Core` package to that feed; all other
packages resolve from nuget.org. This is an intentional trust boundary: the
internal feed is trusted for this package's baseline, not as a general source
for repository dependencies. Restore with the repository `nuget.config` in
place so package validation can resolve the baseline in CI and locally.

## Bumping the baseline

When a release intentionally changes the public API, publish the new package
version first, then update `PackageValidationBaselineVersion` in
`src/Observation.Core/Observation.Core.csproj` to that version. Run the full
build, test, and pack validation before changing the next package version.
The baseline must remain an already-published package version and must be
available from the mapped internal feed.

Package validation runs during `dotnet pack`. A public API removal or incompatible
change should fail the pack step; restore the change and rerun pack before
publishing.
