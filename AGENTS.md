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

<!-- dev-forge:low-interruption:start version=5 -->
## Low-Interruption Execution

- Treat explicit outcome requests such as "fix," "build," "complete," and
  "finish" as continuing authorization for bounded work toward that outcome.
- Continue through diagnosis, implementation, tests, commits, pushes, review
  feedback, and CI repair without renewed confirmation.
- New defects discovered within the same task or pull request remain in scope
  only when they are P0/P1 or release-blocking, or while the accepted outcome
  is not yet complete. After acceptance criteria pass, P2-and-lower
  non-blocking findings become linked follow-ups unless the owner explicitly
  keeps them in the active slice.
- Progress updates are informational and do not pause execution.
- Do not request confirmation when the only realistic alternatives are the
  clearly supported action and inaction.
- Use MAID (Multiple-Choice Agentic Interface Development) only for a real
  operator fork. Lead with the recommended option, label it, make choices
  mutually exclusive, and describe what each choice causes.
- Use the current host's native picker when it is available in the current
  mode. Never invoke a picker that the current host or mode does not expose.
  A missing picker is not a blocker: at a genuine impasse, present two or three
  concise, lettered **A**/**B**/**C** choices instead and request the letter.
- Scale a checkpoint to what it costs the operator, not to how important the
  decision feels. A native picker is cheap enough for any real fork; a typed
  fallback remains reserved for a genuine impasse. Neither is a reason to ask
  for reassurance, re-ask a settled decision, or ask what the repository can
  answer.
- Do not offer a picker whose options are an action and its absence. Do not use
  "Done — continue" as a generic permission gate.
- A picker selects a path; it never authorizes merge, deploy, destructive work,
  secret or access changes, material cost, external communication, or credible
  downtime or data-loss risk. Use the applicable approval control for those
  protected actions.
- End every handback with selectable, genuine next steps when a real next-step
  fork remains. Use the native picker when available; otherwise use lettered
  choices. If no second genuine option exists, state the actual status plainly
  and stop rather than inventing a placeholder choice.
- This governs the form of a handback, not the frequency of checkpoints. Do not
  interrupt bounded work more often because of it. Do not add selectable
  options to a machine-readable output constrained by an external schema.
<!-- dev-forge:low-interruption:end -->
