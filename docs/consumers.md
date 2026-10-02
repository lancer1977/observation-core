# Consumer guide

Observation Core is the evidence layer. It is deliberately unaware of the
domain that captured an observation or the action a consumer may take.

**Observation produces knowledge; consumer policy decides actions.**

**Domain-specific capture, model, or device code (OCR, VLM, OpenCV, FFmpeg,
HDHomeRun, emulators, game/TV/media concepts) must never be added to this
repository.**

## Boundaries

| Observation Core | Observer implementation | Application policy |
| --- | --- | --- |
| Validated `Observation<T>`, `ObservationScope`, confidence, timestamps, source metadata, provenance, deterministic fusion, and bounded cache reuse | Capture and parsing, model/device integration, domain value types, input fingerprints, parser versions, and mapping domain keys to scope segments | Whether evidence is actionable, how `Unknown`/`Disputed` affects a workflow, context transitions, retries, authorization, notifications, and actions |

The dependency direction is `consumer -> Observation.Core`. The library does
not make hidden network calls, publish events, or authorize actions.

## Create an observation

The consumer supplies the value type, evidence ID, scope, subject, predicate,
source metadata, confidence, and timestamp. IDs should be stable enough for a
consumer to trace the evidence back to its source.

```csharp
using Observation.Core;

var observedAt = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
var observation = new Observation<string>(
    Guid.Parse("11111111-1111-1111-1111-111111111111"),
    ObservationScope.Of("sensor", "north"),
    "door",
    "state",
    "open",
    "reader-1",
    "detector",
    0.9,
    observedAt);
```

Confidence is finite and bounded inclusively in `[0, 1]`. Record the original
source IDs and preserve them through adapters and fusion. Fusion reports the
mean confidence of the contributing evidence; consumers must not inflate
confidence merely because an observation passed through fusion.

## Fuse observations

Fusion groups by the exact `(Scope, Subject, Predicate)` tuple. The following
two observations agree, so the result is `Agreed` and its confidence is the
arithmetic mean (`0.85`).

```csharp
using Observation.Core;

var observedAt = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
var scope = ObservationScope.Of("sensor", "north");
var observations = new[]
{
    new Observation<string>(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        scope, "door", "state", "open", "reader-1", "detector", 0.8, observedAt),
    new Observation<string>(
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        scope, "door", "state", "open", "reader-2", "detector", 0.9, observedAt)
};

var result = new ObservationFusion<string>().Resolve(observations).Single();
// result.Status == FusionStatus.Agreed
// result.Value == "open"
// result.Confidence == 0.85
// result.EvidenceIds contains both input IDs
```

Use `FusionStatus.Unknown` when no usable evidence remains, and
`FusionStatus.Disputed` when usable candidates conflict without the configured
confidence margin. Both require consumer policy; neither is authority to act.
`Resolved` is still evidence, not authorization. Candidates and evidence IDs
are retained so a consumer can explain disagreement.

## Cache parsed evidence

The cache stores caller-supplied parsed values. It does not fetch, parse,
refresh, or choose actions. A cache hit preserves the value, timestamp, and
optional evidence ID.

```csharp
using Observation.Core;

var observedAt = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
var observation = new Observation<string>(
    Guid.Parse("11111111-1111-1111-1111-111111111111"),
    ObservationScope.Of("sensor", "north"),
    "door", "state", "open", "reader-1", "detector", 0.9, observedAt);
var cache = new ObservationCache<string, Observation<string>>(clock: () => observedAt);
var request = new ObservationCacheRequest<string>(
    "frame-001", observation.Scope, "sha256:frame-001", "detector-1", TimeSpan.FromMinutes(1));
var cached = await cache.GetOrCreateAsync(
    request,
    (_, _) => Task.FromResult<ObservationCacheValue<Observation<string>>?>(
        new(observation, observation.ObservedAt, observation.Id)));
// cached.Evidence.Disposition == ObservationCacheDisposition.Miss
// cached.Value == observation
// cached.Evidence.EvidenceId == observation.Id
```

Treat `Unknown`, `Expired`, and `Invalidated` cache results as no authority:
they do not provide trustworthy reusable evidence. Consumers decide whether to
capture or parse again. A `Hit` is reusable evidence, not permission to act.

## Scope encoding

Consumers map domain keys to an ordered sequence of non-blank segments. Scope
segments are compared element by element, so do not concatenate levels into a
single ambiguous string. Optional or blank values must be represented by a
labeled/prefixed non-blank segment chosen by the consumer. For example,
AgenticGameHarness encodes a present session as `"s:" + session` (so a blank
session stays a valid segment) and drops the segment for an absent session, which
keeps null and blank distinct. Observation Core does not assign meaning to that
prefix.
