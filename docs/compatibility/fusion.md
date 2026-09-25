# Observation Core fusion compatibility

Issue: [observation-core #6](https://github.com/lancer1977/observation-core/issues/6)

The fixtures in `tests/Observation.Core.Tests/Compatibility/Fusion/Fixtures`
capture the selected behavior of the reference fusion policy without loading or
referencing the reference assemblies. The runner constructs
`ObservationFusion<string>` with `StringComparer.Ordinal` for both value
equality and value ordering, using the raw JSON text as the value. It compares
only the observable subset: scope, subject, predicate, status, value text,
confidence, and ordered evidence IDs.

## AGH test mapping

| AGH source test | Fixture(s) | Status | Compatibility case |
| --- | --- | --- | --- |
| `Resolve_AgreesAndRetainsAllEvidence` | `agreement-provenance.json` | Ported | Agreement averages confidence and retains every provenance ID. |
| `Resolve_SelectsMarginWinnerOrDisputesTie` | `clear-margin-resolution.json`, `near-tie-dispute.json`, `exact-margin-resolution.json` | Adapted | The original method contains three assertions; each is a standalone portable fixture. |
| `Resolve_NeverCombinesDifferentGameOrSessionScopes` | `scope-isolation.json` | Adapted | Context levels are represented by neutral scope segments and remain separate. |
| `Resolve_IsInputOrderIndependentAndValidatesMargin` | `group-ordering.json` | Adapted | The ordering portion is fixture-backed; margin constructor validation remains covered by core unit tests. |

Each fixture is also run with three fixed input permutations. This verifies
input-order independence while keeping the test deterministic.

## Known intentional differences

1. The reference cache's automatic context invalidation and relevant-action
   policy are consumer behavior. Observation Core exposes `InvalidateScope` and
   `InvalidateTag`; the consumer wrapper is tracked by [AGH #346](https://github.com/lancer1977/agentic-game-harness/issues/346).
2. The reference compares values by raw JSON text and orders ties by ordinal
   raw text. These fixtures preserve that behavior explicitly by passing
   `StringComparer.Ordinal` for both comparers. The adapter follow-up is
   [AGH #345](https://github.com/lancer1977/agentic-game-harness/issues/345).
3. Observation Core additionally supports `MaxAge` exclusion and `Unknown`.
   These compatibility fixtures leave `MaxAge` unset because the reference
   cases have no equivalent behavior.
4. Observation Core results include `Candidates` and
   `ExcludedEvidenceIds`. Compatibility assertions intentionally compare only
   the reference-observable subset listed above.

## Not ported

The reference null-session case is not represented. Encoding an absent scope
level is an open consumer decision and is intentionally outside this fixture
slice. No reference assembly is required to build or execute these tests.
5. The reference fusion key is (`GameId`, `SessionId`, subject, predicate) only;
   Observation Core keys on the full `ObservationScope`. An adapter (AGH #345)
   must pass a scope made of exactly the reference's fusion key levels
   (for example `[game, session]`) to preserve grouping; extra scope levels such
   as scene or slot would split groups that the reference merges. The fixtures
   here vary only their first two segments for that reason.

## Verification against the reference

All six fixtures were also executed against the reference `ObservationFusionPolicy`
(via a throwaway harness outside this repository, mapping `scope[0]` to `GameId`
and `scope[1]` to `SessionId`); every expected result matched. Re-run that
comparison before changing a fixture's expected values.
