# Observation cache compatibility

These fixtures capture the portable cache behavior needed by the AgenticGameHarness
extraction. They use only `Observation.Core`; the test project has no reference to
AgenticGameHarness assemblies. Context-like identifiers are opaque scope segments,
for example `ctx-a/run-1/zone-1/slot-x`.

## AGH cache test mapping

| AGH source test | Observation.Core compatibility case | Status | Notes |
|---|---|---|---|
| `ReusesActualEnvelopeAndNotebookEvidenceWithoutRefreshingItsAge` | `Compat_ReusesValueAndEvidenceIdWithoutRefreshingObservedAt` | adapted | Uses a neutral `Observation<string>` and proves same value instance, timestamp, and evidence ID; notebook integration is consumer-specific. |
| `MissingStaleFutureAndExactExpiryAreUnknownWithoutReturningOldValue` | `Compat_MissingStaleFutureAndExactExpiryAreUnknownWithoutOldValue` | ported | Uses an injected clock and checks missing, stale, future, and exact-expiry evidence. |
| `RelevantInvalidationCancelsPendingAndCannotPublishLateValue` | `Compat_TagInvalidationCancelsPendingAndRejectsLateValue` | adapted | AGH relevant-action invalidation is represented by the upstream tag primitive. |
| `PreCancelledRequestDoesNotInvokeParserAndParserVersionBacktrackMisses` | `Compat_PreCancelledRequestSkipsParserAndVersionBacktrackReparses` | ported | The pre-cancelled parser is never invoked; returning to the earlier parser version reparses. |
| `SceneAndSessionTransitionsInvalidateEarlierEvidence` | `Compat_ScopePrefixInvalidationExpressesContextTransitionPolicy` | adapted | Tests `InvalidateScope` on a parent prefix; automatic transition detection remains an AGH wrapper policy. |
| `SlotsHaveBoundedFifoEntriesAndBookkeeping` | `Compat_MultipleSlotsRemainWithinFifoAndBookkeepingBounds` | adapted | Slots are neutral scope segments and assert FIFO eviction plus `Count`, `PendingCount`, and `TrackedScopeCount`. |
| `CallerCancellationBoundsUncooperativeParserAndNeverCachesLateResult` | `Compat_CallerCancellationBoundsUncooperativeParserAndRejectsLateResult` | ported | Uses a gated parser and no time-based wait. |
| `ParserExceptionReleasesAllBookkeepingAndMutableActionsCannotChangeStoredDependencies` | `Compat_ParserExceptionCleansBookkeepingAndTagsAreCopied` | adapted | Relevant actions become copied opaque tags. |
| `EvidenceIdRemainsUsableByActualNotebook` | `Compat_CachedObservationPreservesSameInstanceAndEvidenceId` | adapted | Notebook persistence is not portable; the shared contract preserves the observation instance and ID for a consumer adapter. |
| `MissingInputInvalidatesEarlierReuse` | `Compat_MissingInputInvalidatesEarlierReuse` | ported | Missing input returns `Unknown` and removes earlier reusable evidence. |

## Known intentional differences

1. AGH automatically invalidates on context transitions and has
   `RelevantActions`. Those are consumer policy. Observation.Core exposes
   `InvalidateScope` and `InvalidateTag`; the AGH wrapper follow-up is
   [AGH #346](https://github.com/lancer1977/agentic-game-harness/issues/346).
2. Fusion-only upstream differences—`MaxAge` exclusion, `Unknown` status, and
   extra result fields—are outside this cache slice. The fusion compatibility
   fixtures must use the explicitly documented AGH adapter comparers and assert
   only the AGH-observable subset; see [AGH #345](https://github.com/lancer1977/agentic-game-harness/issues/345).
3. Cache results include shared metadata such as `EvidenceId`; downstream
   notebook/action interpretation remains outside Observation.Core.

## Not ported

- The AGH null-session case is not encoded as a scope fixture. The segment
  representation for an absent session is an open decision for the AGH adapter
  and is intentionally left to that follow-up.
- Automatic game/session/scene transition detection and notebook eligibility
  are not upstream behaviors; their adapters remain consumer-owned.
