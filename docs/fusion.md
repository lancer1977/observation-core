# Deterministic observation fusion

`ObservationFusion<T>` groups observations by the exact tuple `(Scope, Subject,
Predicate)`. Scope segments are compared element by element; a scope and one of
its descendants are separate groups.

## Configuration

| Option | Default | Behavior |
| --- | --- | --- |
| `MinimumConfidenceMargin` | `0.2` | Must be finite and within `[0, 1]`. A leader resolves a conflict when its mean confidence minus the runner-up's mean is at least this value. |
| `MaxAge` | `null` | When set, evidence with age `>= MaxAge` or a timestamp in the future is excluded. A negative age limit is invalid. |
| `Clock` | `null` unless expiry is enabled | Supplies the timestamp for expiry checks. Expiry-enabled options use UTC now when no clock is supplied. |
| `ValueEquality` | `EqualityComparer<T>.Default` | Defines when observations contribute to the same candidate. |
| `ValueOrder` | `Comparer<T>.Default` | Orders equal-confidence candidates. Unordered types must supply a comparer. |

## Results

| Evidence | Status | Value | Confidence | Candidates |
| --- | --- | --- | --- | --- |
| No usable evidence | `Unknown` | default | `0` | empty |
| One distinct candidate | `Agreed` | candidate value | candidate mean | all candidates (one) |
| Multiple candidates, margin met | `Resolved` | leader value | leader mean | every candidate |
| Multiple candidates, margin not met | `Disputed` | default | leader mean | every candidate |

Candidate confidence is the arithmetic mean of its contributing observations.
All evidence IDs, including minority evidence, are retained and sorted in
ascending `Guid` order. Excluded IDs are reported separately, also sorted.
Candidate order is confidence descending, then `ValueOrder` ascending, with the
smallest evidence ID as a final deterministic tie-break. Results are ordered by
scope, subject, and predicate using ordinal comparison; input order never
changes the output.

The operation is single-threaded, pure, and performs no I/O. It does not merge
prefix scopes or interpret values, subjects, predicates, or source metadata.
