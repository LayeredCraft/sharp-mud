# [ADR-0003] Allow `appsettings.json` for Non-Secret Configuration

**Status:** Accepted

**Date:** 2026-07-16

**Decision Makers:** solo

## Context

`security.md` has stated, since early in this repo, "No `appsettings.json`,
and no secrets committed to the repo" — all configuration (`SHARPMUD_MODE`,
`SHARPMUD_TELNET_PORT`, `SHARPMUD_DB_PATH`) is env-var-only, hand-parsed in
`HostOptions.Parse`.

Adding Serilog (ADR-0002) surfaced a real gap in that rule: `coding-standards.md`
already documents the `IOptions<T>` pattern (`Options` suffix,
`SectionName` const, `services.Configure<T>(configuration.GetSection(...))`)
as the standard for configuration classes — but that pattern is built
specifically around combining `appsettings.json` + environment variables +
in-code defaults via `Microsoft.Extensions.Configuration`. With no
`appsettings.json` allowed, half of that documented pattern had nowhere to
attach, and Serilog's own idiomatic configuration story
(`Serilog.Settings.Configuration`, reading a `Serilog` JSON section) was
unusable without a real config file.

## Decision Drivers

- The original "no `appsettings.json`" rule was motivated by "no secrets
  committed with real values," not by an objection to configuration files
  in general — env vars and `appsettings.json` are both just delivery
  mechanisms; the actual risk is a *committed file containing a real
  secret value*, not the file format.
- `coding-standards.md`'s `IOptions<T>` conventions already assume
  `appsettings.json` exists; leaving the old rule in place made that
  section of the standards effectively dead guidance.
- Serilog's ecosystem is built around `Serilog.Settings.Configuration`
  reading a `Serilog` JSON section — fighting that with pure code
  (`LoggerConfiguration` fluent API only) means reinventing what the
  library already does well.

## Considered Options

1. Keep the no-`appsettings.json` rule; configure Serilog entirely via the
   fluent `LoggerConfiguration` API in code.
2. Allow `appsettings.json` for Serilog configuration only, leave
   `HostOptions`/env-var parsing untouched.
3. Allow `appsettings.json` broadly for any non-secret configuration,
   explicitly still forbidding committed secrets — the standard ASP.NET
   Core model of appsettings.json (defaults) + environment variables
   (environment-specific overrides/secrets) + code defaults.

## Decision Outcome

Chosen option: **"3 — allow `appsettings.json` broadly for non-secret
config,"** because scoping it to "just Serilog" would leave the same gap
open for the next `IOptions<T>` class, and the underlying rationale
(secrets never committed) applies the same way regardless of which
subsystem's configuration it is.

**What changes:**
- `appsettings.json` may exist and be committed, for **non-secret**
  configuration values (log levels, feature toggles, tunable defaults).
- **Secrets never go in `appsettings.json`** (or any committed file) with
  a real value — connection strings with credentials, API keys, anything
  that would be a real compromise if leaked, stay on environment variables
  only, exactly as before. This is not a relaxation of the secrets rule,
  only of the "no config file at all" rule.
- The `IOptions<T>` pattern in `coding-standards.md` is now fully usable
  as originally documented: `services.Configure<TOptions>(configuration.GetSection(TOptions.SectionName))`
  against a real `IConfiguration` built from `appsettings.json` +
  environment variables + code defaults, in that override order (standard
  ASP.NET Core precedence — env vars win).
- `HostOptions.Parse`'s existing hand-rolled env-var parsing is **not**
  migrated in this ADR — that's a separate, later decision if/when it's
  worth converting to the `IOptions<T>` pattern. This ADR unblocks that
  conversion; it doesn't perform it.

### Positive Consequences

- `coding-standards.md`'s `IOptions<T>` section is no longer dead guidance.
- Serilog can use its own idiomatic `Serilog.Settings.Configuration`
  reader instead of a hand-maintained fluent-API mirror of the same
  settings.
- Future configuration (feature toggles, tunable gameplay constants) has
  an established home that isn't "hardcode it or add an env var for
  everything."

### Negative Consequences

- Two configuration mechanisms now coexist (`HostOptions.Parse`'s manual
  env-var parsing, and `IConfiguration`-backed `appsettings.json`/env
  vars) until/unless `HostOptions` is migrated — a new contributor has to
  know both exist and why, at least until that follow-up decision is made.
- A committed `appsettings.json` is one more place a future contributor
  could accidentally paste a real secret — mitigated by the rule being
  explicit and by `security.md` continuing to say committed secrets are
  never acceptable, in any file.

## Links

- `.agents/skills/engineering-workflow/references/security.md` — updated
  by this decision.
- `.agents/skills/engineering-workflow/references/coding-standards.md` —
  the `IOptions<T>` section this decision makes fully usable.
- [ADR-0002](0002-telnet-protocol-negotiation.md) — the Serilog work that
  surfaced this gap.
