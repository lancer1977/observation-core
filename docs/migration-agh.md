# AgenticGameHarness migration

This document records the completed migration pattern. AgenticGameHarness
fusion ([#345](https://github.com/lancer1977/agentic-game-harness/issues/345))
and cache ([#346](https://github.com/lancer1977/agentic-game-harness/issues/346))
now run on Observation Core. The originating consumer work is tracked by
[AGH #342](https://github.com/lancer1977/agentic-game-harness/issues/342).

The adapter keeps the boundary thin:

1. Map the AGH envelope to `Observation<string>` and map the resulting
   `FusionResult<string>` or cached value back to the envelope shape.
2. Encode the fusion scope as `[game]` or `[game, "s:" + session]`. The
   segments are ordered and non-blank; the `s:` prefix distinguishes the
   optional session level without adding a null/blank segment.
3. Construct `ObservationFusion<string>` with `StringComparer.Ordinal` for
   both `ValueEquality` and `ValueOrder`. This preserves the reference
   behavior for raw serialized values and makes tie ordering culture
   independent.
4. Keep game context transitions and `RelevantActions` in the AGH wrapper.
   Relevant actions become tags, and the wrapper applies scope/tag
   invalidation around the core cache; they are not behaviors of Observation
   Core.
5. Keep the pub/sub bus in AGH. Observation Core is synchronous/pure for
   fusion and does not publish events or select actions.

The compatibility fixtures in [`docs/compatibility/fusion.md`](compatibility/fusion.md)
and [`docs/compatibility/cache.md`](compatibility/cache.md) show the portable
subset and intentional differences.
