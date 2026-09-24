# Observation.Core contracts

Observation.Core is a domain-neutral evidence layer. It represents observations,
confidence, provenance, disagreement, fusion, and bounded reuse. **Observation
produces knowledge; consumers decide actions.** The library contains no
consumer or domain concepts, policy, action authorization, hidden network calls,
or media/device processing dependencies.

## `ObservationScope`

An `ObservationScope` is an immutable, ordered sequence of one or more opaque
string segments. Segments are compared element by element with ordinal string
semantics; they are not concatenated. This preserves boundaries:
`["a", "b/c"]` is different from `["a/b", "c"]`, and `["a", "b"]` is
different from `["ab"]`. `ToString()` joins with `/` for diagnostics only and
is not the wire representation.

| API | Contract |
| --- | --- |
| `Of(params string[] segments)` | Requires a non-null array with at least one non-blank segment; throws `ArgumentNullException` for a null array and `ArgumentException` for an empty array or blank segment. The input is defensively copied. |
| `Segments` | Read-only ordered segments. |
| `Equals` / `GetHashCode` | Ordinal, element-wise semantics. |
| `CompareTo` | Ordinal element-wise ordering; when one scope is a prefix of the other, the shorter scope sorts first. |
| `IsPrefixedBy(prefix)` | Returns whether this scope begins with every segment in `prefix`; null `prefix` throws `ArgumentNullException`. |
| `Append(segment)` | Returns a new scope with one non-blank segment; blank input throws `ArgumentException`. |

JSON uses an array of strings, for example:

```json
["root", "a/b"]
```

## `Observation<T>`

`Observation<T>` is a sealed, immutable record. Its `Value` type is supplied by
the consumer; the library does not define domain value types. The `Id` is the
durable evidence identifier supplied by the caller. The library rejects an
empty `Guid`, but does not generate IDs or enforce global uniqueness.

| Property | Meaning and validation |
| --- | --- |
| `Id` | Non-empty `Guid` evidence identifier; empty throws `ArgumentException`. |
| `Scope` | Non-null `ObservationScope`; null throws `ArgumentNullException`. |
| `Subject` | Observed subject key; null, empty, or whitespace throws `ArgumentException`. |
| `Predicate` | Observed predicate key; null, empty, or whitespace throws `ArgumentException`. |
| `Value` | Observed consumer-defined value of type `T`; null throws `ArgumentNullException`. |
| `SourceId` | Source identifier; null, empty, or whitespace throws `ArgumentException`. |
| `SourceKind` | Opaque caller-defined source kind; null, empty, or whitespace throws `ArgumentException`. The library assigns no meaning to it. |
| `Confidence` | Finite `double` in the inclusive range `[0, 1]`; invalid values, including `NaN` and infinities, throw `ArgumentOutOfRangeException`. |
| `ObservedAt` | Observation timestamp; `default` throws `ArgumentException`. |

The container exposes get-only properties and validates all constructor inputs.
The immutability of `Value` itself depends on the consumer's `T` type.

Default `System.Text.Json` serialization emits these PascalCase property names:

```json
{
  "Id": "11111111-1111-1111-1111-111111111111",
  "Scope": ["root", "a/b"],
  "Subject": "subject",
  "Predicate": "predicate",
  "Value": "value",
  "SourceId": "source",
  "SourceKind": "opaque",
  "Confidence": 0.75,
  "ObservedAt": "2026-09-24T12:00:00+00:00"
}
```

## Generic consumer guidance

Consumers map their own keys onto scope segments and choose their own encoding
for optional levels. Blank segments are rejected; this library does not
recommend a particular encoding for an absent or optional level.

The following are **consumer code**, not types defined by Observation.Core:

```csharp
// Consumer code: a game-like application defines its own value type.
public sealed record MatchState(int Score);

var observation = new Observation<MatchState>(
    Guid.NewGuid(),
    ObservationScope.Of("session", "one"),
    "match",
    "has-state",
    new MatchState(7),
    "local-reader",
    "state-source",
    0.9,
    DateTimeOffset.UtcNow);
```

```csharp
// Consumer code: a broadcast-like application defines a different value type.
public sealed record ProgramState(string Status);

var observation = new Observation<ProgramState>(
    Guid.NewGuid(),
    ObservationScope.Of("channel", "one"),
    "program",
    "has-state",
    new ProgramState("ready"),
    "feed-reader",
    "program-source",
    0.8,
    DateTimeOffset.UtcNow);
```

## Compatibility and scope

The stable public API documented here is the `Observation.Core` namespace's
`ObservationScope` and `Observation<T>` contracts, including their validation,
ordinal semantics, and JSON shapes. Consumers should treat property names and
scope-array serialization as compatibility-sensitive.

Fusion and cache behavior are separate concerns tracked by issues [#4](https://github.com/lancer1977/observation-core/issues/4)
and [#5](https://github.com/lancer1977/observation-core/issues/5). Pub/sub,
consumer policy, and domain-specific integrations are out of scope for this
contract documentation.
