# Mutation testing

Mutation testing is an on-demand quality check for the semantic behavior in
`src/Observation.Core`. It is not a CI gate.

## Running Stryker

From the repository root:

```sh
dotnet tool restore
dotnet tool run dotnet-stryker --config-file stryker-config.json
```

`stryker-config.json` mutates only `src/Observation.Core` and runs
`tests/Observation.Core.Tests`. The existing property, architecture, and
documentation tests remain enabled; this run did not require a test-case
filter because they passed in Stryker's copied sandbox. Reports are written
under `StrykerOutput/`, which is gitignored and should be removed after a
local run.

The local tool is pinned to `dotnet-stryker` 5.0.0 in
`.config/dotnet-tools.json`.

## Baseline and final scores

| Run | Mutants created | Tested | Killed | Survived | Timeout | No coverage | Mutation score |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Baseline, before focused tests | 375 | 257 | 199 | 58 | 0 | 9 | 74.81% |
| Final, after focused tests | 375 | 260 | 210 | 49 | 1 | 6 | 79.32% |

Compile errors (37) and block-covered mutants (72) were skipped by Stryker in
both runs. The final run completed successfully with no Stryker errors.

## Survivor triage

The focused tests are in
`tests/Observation.Core.Tests/Mutation/FusionAndCacheMutationTests.cs` and
use an injected fixed clock. They kill boundary, candidate construction,
cache tag-union, null-result, FIFO, parser-capacity, and pending-invalidation
mutants without sleeps.

The final report's 49 survivors were triaged as follows. IDs are Stryker's
mutant IDs from the final JSON report.

| Area and mutant IDs | Triage | Reason |
| --- | --- | --- |
| Cache request guards: 8, 9, 10, 11, 12, 14, 22, 26, 31, 32 | Equivalent (assessed, not proven by test) | Argument null/metadata checks and their diagnostic text are already enforced by the request constructor or produce no distinct externally observable result in this call path. |
| Cache hit/unknown bookkeeping: 44, 46, 52, 53, 56, 57, 58, 59, 61, 65, 71, 74, 75, 90, 95, 97, 101, 114, 134 | Equivalent | Removing a statement, changing a reason string, or changing the internal sequence counter does not change the public disposition/value/count under the covered path. Cancellation plus version mismatch is intentionally redundant: either condition yields invalidation. |
| Cache invalidation metadata: 119, 122, 124 | Equivalent | The metadata cleanup block, `LiveScopes` composition, and any positive version increment only maintain internal bookkeeping; the public cache behavior remains unchanged for live entries. |
| Cache invalidation timeout: 127 | Equivalent/timeout | Removing the pending cancellation call leaves the version mismatch guard able to reject an uncooperative late result, but hangs a parser that waits only for cancellation. Stryker reports this as a timeout, not a surviving behavior; the focused token-observing test covers the intended cancellation path. |
| Cache value/request diagnostics: 155, 162 | Equivalent | These are constructor diagnostic-string mutations; validation behavior and exception types are unchanged. |
| Fusion constructor diagnostics: 206, 208, 209 | Equivalent | Changing the unsupported-ordering exception text or removing the null-element guard statement does not alter the tested public result for valid inputs. |
| Fusion candidate construction: 222, 225 | Equivalent | Groups are non-empty by construction, so `First` and `FirstOrDefault` agree. The final `ThenBy(EvidenceIds[0])` tie-break is redundant with LINQ's stable sort: observations are sorted by ID before grouping, so candidates already arrive in ascending first-evidence-ID order. |
| Fusion default ordering: 247, 248 | Equivalent | Nullable unwrapping and the paired comparable-interface check select the same supported ordering behavior for the library's supported value types; custom unordered values must supply `ValueOrder`. |
| Observation validation diagnostics: 261, 266, 277, 285 | Equivalent | Only exception message literals change; validation conditions and exception types remain the same. |
| Observation scope diagnostics/no-op: 292, 303, 305, 329 | Equivalent | Only exception text or a bookkeeping-free statement changes; scope validation and comparison behavior are unchanged. |

No Stryker survivor exposed a real defect in `src/`; no source changes were
needed. The six remaining no-coverage mutants are outside the focused triage
set and are recorded in the generated report for future test expansion.

Equivalence judgments above were made by reading the code, not by an automated
proof; a future reader who finds a distinguishing input should turn that survivor
into a test.
