# Bounded observation cache

`ObservationCache<TInput, TResult>` provides bounded, deterministic reuse of
caller-supplied parsed evidence. It does not parse, fetch, refresh, or choose
consumer actions.

| Concept | Behavior |
| --- | --- |
| Key | Typed `ObservationScope` + parser version + fingerprint; segments are never concatenated. Version tracking is per scope, so parser version and fingerprint are both version dimensions of the scope. |
| Freshness | Evidence is usable only when `ObservedAt` is non-default, not future, and `clock - ObservedAt < MaxAge`. Exactly `MaxAge` is expired. |
| Missing evidence | Null input, null parser output, stale/future output, and exhausted pending capacity return `Unknown` without a value. Null input additionally invalidates the scope's earlier entries and pending parsers (missing input means the earlier reuse can no longer be trusted). |
| Invalidation | Scope invalidation matches the requested scope and descendants; tag invalidation matches requests containing that ordinal tag. Both remove entries and cancel pending parsers. |
| Version safety | A change of fingerprint **or parser version** for the same scope advances the scope version and invalidates its earlier entries and pending parsers; old or late parses cannot publish after a change or invalidation. A backtrack to an earlier parser version or fingerprint re-parses. |
| Capacity | Entries use deterministic FIFO eviction. Pending parsers and scope metadata are jointly bounded by the configured capacity. |
| Cancellation | Caller cancellation stops the caller even if a parser ignores cancellation. Invalidation returns `Invalidated`; parser exceptions propagate after bookkeeping cleanup. |
| Evidence | The parsed value, timestamp, and optional evidence ID are returned unchanged; cache hits preserve the evidence ID. |

Consumers choose their own mapping. For example, a consumer may encode its
session, scene, and independent slot as scope segments such as
`["session", sessionKey, "scene", sceneKey, "slot", slotKey]`, and put the
consumer's chosen invalidation dependencies (including an action set) in tags.
This cache does not prescribe whether those levels exist, how they are named,
or which tags a consumer should use.
