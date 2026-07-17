# [ADR-0002] Telnet Protocol Negotiation (IAC/Q-Method core + NAWS)

**Status:** Accepted

**Date:** 2026-07-16

**Decision Makers:** solo

## Context

Per ADR-0001, this is Slice 1 of the WheelMUD reconciliation roadmap.
sharp-mud's `TelnetSession` currently does raw TCP + line-based I/O with
IAC (0xFF) byte sequences stripped from input and discarded — there is no
actual telnet option negotiation. `docs/networking.md`'s Open Items has
carried "MCCP/MXP/NAWS telnet protocol negotiation — deferred" since the
adapter was first built, citing WheelMUD's `Server/Telnet/` as the
reference to consult when it's actually needed.

WheelMUD's real implementation (~1,400 negotiation-specific lines across
18 files, plus ~370 lines of hosting glue) implements the RFC-1143
"Q-Method" of telnet option negotiation: every option tracks four state
variables (`Us`/`Him` sides, each `NO`/`WANTNO`/`YES`/`WANTYES`, plus a
one-deep queued-opposite flag) to avoid infinite negotiation loops. On top
of that core, it implements five options: Echo (RFC 857), NAWS (RFC 1073,
window size), TermType (RFC 1091), MCCP2 (compression), and MXP. NAWS
clamps client-reported width/height to a 20×6 floor — a directly
security-relevant detail, since Telnet is documented in
`.agents/skills/engineering-workflow/references/security.md` as an
untrusted/adversarial input surface, and an unclamped size feeds directly
into future word-wrap/paging logic.

## Decision Drivers

- NAWS (terminal size) is the option sharp-mud most immediately needs, for
  future word-wrap/paging/display logic — the other options are lower
  value (MCCP: bandwidth barely matters today; MXP: WheelMUD's own code
  admits its parser is ad hoc and client support is narrow; TermType:
  minor).
- sharp-mud's read loop is already a single sequential `await` chain per
  connection (unlike the environment WheelMUD's parser classes were
  originally written for) — a leaner reimplementation of the byte parser
  is possible without losing correctness.
- The four-state Q-Method tracking itself is not a stylistic choice — it's
  the mechanism that prevents negotiation loops, and must be preserved
  even in a leaner reimplementation.
- `coding-standards.md` (already established this session) sets real
  constraints: sealed classes, traditional constructors, `internal` by
  default, 4-param-max methods, structured logging via Serilog +
  `LayeredCraft.StructuredLogging` — this repo's *first* real logging
  infrastructure, since none exists today.
- `TelnetSession`/`TelnetListener` currently use primary constructors
  (pre-existing debt per `coding-standards.md`'s known-inconsistency
  note) — this work substantially touches both files, which sanctions
  migrating them to traditional constructors as part of this change.
- `HostRunner.RunTelnetAsync` already takes 7 parameters, over the 4-param
  rule even before this change — adding a logger dependency pushes it to 8
  unless addressed now.

## Considered Options

1. **Skip negotiation entirely, keep byte-stripping as-is.** Rejected —
   this is the status quo the roadmap exists to move past; no terminal
   size, no path to future display features.
2. **Faithful port of WheelMUD's class shape** (`TelnetOption` base class +
   per-option subclasses + `ConnectionTelnetState` 5-class byte-parser
   hierarchy), translated to sharp-mud's naming/access-modifier
   conventions but keeping the same architecture.
3. **Leaner reimplementation**: keep the RFC-1143 four-state tracking
   (correctness-critical, adopted near-verbatim), replace the persistent
   5-class byte-parser state machine with a single compact method that
   parses a whole IAC sequence per call, leaning on the read loop already
   being one sequential `await` chain per connection.

## Decision Outcome

Chosen option: **"3 — leaner reimplementation,"** because it preserves
the only genuinely correctness-critical piece (the Q-Method state
tracking) while avoiding a persistent cross-call parser-state object that
sharp-mud's execution model doesn't need — the same pattern sharp-mud
already used when it adopted WheelMUD's event system (one generic
`Publish<TEvent>`/`IsCanceled` mechanism instead of ~8 duplicated
delegate pairs, per `wheelmud-findings.md`).

**Scope for this PR**: IAC negotiation core + NAWS. Echo is folded into
the same negotiator (removes a latent desync risk versus the existing
hand-rolled `SetEchoAsync`, with zero observable behavior change to
callers). MCCP, MXP, and TermType are explicitly deferred to future ADRs.

This decision also requires standing up this repo's first logging
infrastructure (adding an `ILogger<TelnetSession>` dependency means
Serilog + `LayeredCraft.StructuredLogging` need to exist, not just be
documented in `coding-standards.md`), and fixes a pre-existing
parameter-count violation on `HostRunner.RunTelnetAsync` (already 7
params before this change, over the 4-param rule) via a `TelnetHostContext`
parameter object, since that signature is already being touched to thread
the logger through.

The full execution breakdown — every new/modified file, the exact
negotiator API, the logging wiring, the parameter-object shape, and the
test plan — lives in [PLAN-0002](../plans/0002-telnet-protocol-negotiation.md),
not here. This ADR fixes *what* was decided; the plan tracks *how* it gets
built and is expected to change as implementation proceeds.

### Positive Consequences

- Real terminal-size awareness for the first time, unblocking future
  display features.
- This repo's first logging infrastructure, standing on a real feature
  rather than being introduced in the abstract.
- Fixes a pre-existing parameter-count violation (`RunTelnetAsync`) while
  already touching that signature, instead of compounding it.
- Removes a latent Echo-negotiation desync risk as a side effect of
  folding it into the shared negotiator.

### Negative Consequences

- Adds a new subdirectory/six new internal types for what is, in NAWS
  terms, a small feature — justified by making MCCP/MXP/TermType additive
  in a later slice rather than a redesign, but a real complexity increase
  over the previous 2-method `TelnetSession`.
- Logging infrastructure decisions made here (Serilog, console-only sink)
  become the de facto standard for the rest of the repo without a
  dedicated "adopt logging repo-wide" design pass — acceptable since it's
  a small, reversible surface (one sink, one bridge package), but worth
  flagging.

## Pros and Cons of the Options

### Option 2: Faithful port of WheelMUD's class shape

- Good, because it's a direct, low-judgment translation with less risk of
  introducing a subtle bug in the RFC 1143 logic.
- Bad, because the 5-class persistent byte-parser hierarchy solves a
  cross-call-state problem sharp-mud's single-`await`-chain read loop
  doesn't have — porting it faithfully would be carrying over unnecessary
  complexity, not fidelity.

### Option 3: Leaner reimplementation (chosen)

- Good, because it matches sharp-mud's established pattern of simplifying
  WheelMUD's designs where the extra structure doesn't earn its keep.
- Good, because fewer types means less surface for the *other* new thing
  in this PR (logging, parameter-object refactor) to interact badly with.
- Bad, because it requires actually understanding *why* the Q-Method
  state machine works, rather than transcribing it — mitigated by copying
  the transition-table logic directly from WheelMUD's source rather than
  re-deriving it from the RFC.

## Links

- [ADR-0001](0001-wheelmud-reconciliation-roadmap.md) — WheelMUD
  Reconciliation Roadmap (this is Slice 1).
- [PLAN-0002](../plans/0002-telnet-protocol-negotiation.md) — execution
  plan for this decision.
- `docs/research/wheelmud-findings.md` — `Server/Telnet/` citation this
  ADR resolves.
- `docs/networking.md` — Open Items this ADR closes (NAWS) and narrows
  (MCCP/MXP/TermType).
- `.agents/skills/engineering-workflow/references/security.md` — "Telnet
  is untrusted input" rule the NAWS clamping directly implements.
- `WheelMUD/src/Server/Telnet/TelnetOption.cs`,
  `WheelMUD/src/Server/Telnet/TelnetOptionNaws.cs` — source being adapted.