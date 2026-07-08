# Design Docs Index

`SPEC.md` (repo root) is the vision/decisions doc — read that first for *what*
and *why*. These docs are the granular *how*, one file per subsystem. Update
both together when a decision changes; SPEC.md wins on intent, these win on
implementation detail.

| Doc | Covers |
|---|---|
| [architecture.md](architecture.md) | Project/solution structure, dependency direction, DI, the global tick loop, testing & observability conventions |
| [engine-vs-ruleset.md](engine-vs-ruleset.md) | **Start here for the entity model.** `Thing`/`Behavior` composition, the event system, engine-vs-ruleset project split — supersedes the concrete classes described in world-model.md/character.md/combat.md, which still describe the game *shape* |
| [world-model.md](world-model.md) | Room/Exit/Area data model, hand-built hub vs. generated frontier, generation-on-demand flow |
| [character.md](character.md) | Player entity, D&D-style attributes, Race/Class modifiers, derived stats |
| [commands.md](commands.md) | Command parser/registry, `ICommand` pipeline, aliases, error handling, movement walkthrough |
| [combat.md](combat.md) | Round-based combat model, `ICombatResolver`, tick-driven resolution, disconnect-mid-fight handling |
| [persistence.md](persistence.md) | Repository interfaces, EF Core, provider strategy (SQLite now, Mongo/DynamoDB later) |
| [networking.md](networking.md) | `ISession` transport abstraction, adapter plan (CLI now, Telnet/SSH/WebSocket later) |
| [accounts-auth.md](accounts-auth.md) | External OAuth login, identity fields on Player |
| [research/wheelmud-findings.md](research/wheelmud-findings.md) | WheelMUD codebase review with code citations — the prior art behind engine-vs-ruleset.md |

## Open items across all docs

Each doc has its own "Open Items" section for decisions still needed in that
area. Cross-cutting ones:

- .NET 11 preview risk: if tooling/package ecosystem (esp. any EF Core
  provider) lags preview support, fall back to .NET 10 LTS.
- Soft-code/scripting engine: deferred per SPEC.md, not designed in any doc yet.
- Moderation/admin tooling: deferred per SPEC.md, not designed in any doc yet.
