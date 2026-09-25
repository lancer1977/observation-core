# SmartTVRelay adoption guidance

This is guidance for the proposed SmartTVRelay adoption tracked by
[SmartTVRelay #1](https://github.com/lancer1977/SmartTVRelay/issues/1). It is
not implemented in Observation Core, and it does not add any TV concepts to
this repository's code.

SmartTVRelay can keep its commercial/program detector implementations and
emit `Observation<T>` values from those detectors. The value type `T`, source
IDs, source kinds, subjects, predicates, timestamps, and confidence remain
owned by SmartTVRelay. Each detector should attach a traceable evidence ID
and report confidence in the inclusive `[0, 1]` range.

Choose a stable ordered scope mapping such as:

```text
[channelKey, programKey]
```

Use non-blank, labeled segments if a level is optional. Keep channel and
program identity in scope only when that is the intended fusion boundary;
Observation Core never merges a scope with a descendant scope. Fuse matching
observations with `ObservationFusion<T>`, choose deterministic equality/order
comparers when `T` needs them, and preserve the returned status, candidates,
confidence, and evidence IDs for diagnostics.

SmartTVRelay remains responsible for interpreting `Unknown` and `Disputed`,
deciding whether evidence is sufficient, and authorizing any stream-switching
or other application action. Detector inputs, commercial/program semantics,
pub/sub, retries, and action policy stay in SmartTVRelay.

