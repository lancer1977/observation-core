# AGENTS.md

## Purpose
Observation Core is the shared, domain-neutral evidence layer used by multiple consumers.

The library represents observations, confidence, provenance, disagreement, fusion, and bounded reuse.

**Observation produces knowledge. Consumers decide actions.**

## Hard boundaries
Do not add:
- game-specific concepts
- TV/media-specific concepts
- OCR, VLM, OpenCV, FFmpeg, HDHomeRun, emulator, or device dependencies
- action authorization, stream switching, or consumer policy
- hidden network/service calls

If a task appears to require one of these, stop expanding scope and document the dependency in the issue/PR.

## Change discipline
- Work only the assigned issue.
- Prefer the smallest complete change.
- Do not redesign adjacent APIs unless required by acceptance criteria.
- Do not opportunistically rename/reformat unrelated code.
- Preserve public behavior unless the issue explicitly authorizes a breaking change.
- New public API requires tests and documentation.

## Required validation
Before marking work complete:
1. restore/build succeeds
2. unit tests pass
3. relevant invariant/compatibility tests pass
4. package creation succeeds when packaging is in scope
5. no forbidden consumer/domain dependency was introduced

Run the repository-provided validation commands once they exist; do not invent a parallel build path.

## Core invariants
Changes must preserve these unless the issue explicitly changes them:
- confidence is bounded and validated
- fusion is deterministic and input-order independent
- disagreement is preserved rather than silently discarded
- contributing evidence/provenance remains traceable
- expired/invalid evidence cannot influence a result
- stale/late cache work cannot overwrite newer scope state
- bounded structures remain bounded

## Tests
Prefer deterministic tests.
Use fixtures for compatibility behavior.
For fusion/cache work, include edge cases for no evidence, agreement, conflict, near-ties, expiration, cancellation, invalidation, and ordering where relevant.

Property-based and mutation tests are encouraged for semantic code but must not replace focused unit tests.

## PR evidence
PRs should include:
- issue reference
- summary of behavior changed
- tests added/updated
- commands run and result
- compatibility/breaking-change notes
- any follow-up intentionally left out of scope

## Dependency direction
Allowed:
consumer -> Observation Core

Forbidden:
Observation Core -> consumer

Consumers include AgenticGameHarness, SmartTVRelay, and any future application/domain repository.
