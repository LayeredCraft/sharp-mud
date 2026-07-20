# [ADR-0007] Narrow the `SharpMud` Meta-Package to Engine + Hosting + Persistence

**Status:** Accepted

**Date:** 2026-07-20

**Decision Makers:** solo

## Context

[ADR-0006](0006-nuget-package-distribution.md) shipped `SharpMud` as a
meta-package with `ProjectReference`s to *every* other `SharpMud.*` package —
`Engine`, `Hosting`, `Persistence`, both persistence providers
(`.Sqlite`/`.DynamoDb`), and both transport adapters (`Adapters.Cli`/
`.Telnet`). That was a deliberate choice at the time ("no `PackageId`
overrides are needed except on the new meta-package... `ProjectReference`s to
everything above").

In PR review of the docsite Getting Started page, this surfaced a real
problem: a consumer running `dotnet add package SharpMud` already gets both
persistence providers and both transport adapters transitively, making the
docs' "you still pick a persistence provider and a transport explicitly"
framing false, and defeating the ala-carte flexibility ADR-0006 itself named
as a decision driver. Every consumer pulls in a DynamoDB SDK dependency and
a Telnet listener even if they only ever use SQLite + CLI, which isn't what
"quick start" should mean for a package whose whole pitch is choosing your
own persistence/transport.

## Decision Drivers

- The meta-package should be a fast on-ramp for the *engine-level* pieces
  every consumer needs (Engine, Hosting, provider-agnostic Persistence), not
  a way to sidestep the ala-carte provider/transport choice ADR-0006 itself
  motivated.
- Docs (`getting-started.md`) already describe `SharpMud` as "you still pick
  a persistence provider and a transport" — narrowing the package to match
  is less churn than rewriting every doc/example to match the current
  all-inclusive package.

## Considered Options

1. Keep `SharpMud` including every package (status quo from ADR-0006).
2. Narrow `SharpMud` to `Engine` + `Hosting` + `Persistence` only — a
   consumer still explicitly adds one persistence provider package and one
   (or more) transport adapter package(s).

## Decision Outcome

Chosen option: **2 — narrow the meta-package.** `SharpMud`'s
`ProjectReference`s become `SharpMud.Engine`, `SharpMud.Hosting`, and
`SharpMud.Persistence` only. Persistence providers and transport adapters
are always explicit `dotnet add package` calls, regardless of whether a
consumer also referenced the meta-package.

### Positive Consequences

- A consumer never gets an unused DynamoDB SDK or Telnet listener dependency
  by default.
- `getting-started.md`'s existing "meta-package + explicit provider +
  explicit transport" install steps are now literally accurate instead of
  needing a rewrite.

### Negative Consequences

- One more explicit `dotnet add package` line for the common case (Engine +
  Hosting + Persistence + one provider + one transport is now 5 packages
  instead of 3) — accepted as the direct tradeoff for "no unused
  dependencies by default."

## Pros and Cons of the Options

### Option 1 — keep including everything

- Good, because it's the fewest packages to `dotnet add` for the common
  case.
- Bad, because it silently pulls in both a DynamoDB SDK dependency and a
  Telnet listener for every consumer, regardless of which (if either) they
  actually use.
- Bad, because it contradicts the "ala-carte flexibility" driver ADR-0006
  itself named, and made the getting-started docs describe a package
  contract that wasn't real.

### Option 2 — narrow to Engine + Hosting + Persistence (chosen)

- Good, because it matches the docs' existing framing without a rewrite.
- Good, because it keeps the meta-package genuinely opt-in beyond the
  provider-agnostic core.
- Bad, because the "smallest possible game" quick start needs one more
  explicit package reference than before.

## Links

- [ADR-0006](0006-nuget-package-distribution.md) — this ADR narrows one
  specific decision from ADR-0006 (the meta-package's package set); the
  rest of ADR-0006 (granular packages, `SharpMud.Hosting`, the overall
  distribution strategy) is unchanged and still in effect.
- [getting-started.md](https://github.com/LayeredCraft/sharp-mud/blob/main/docsite/docs/getting-started.md) —
  the install guidance this decision keeps accurate.
