# sharp-mud

A modern C#/.NET engine for building MUDs (Multi-User Dungeons) — faithful to
the genre's feel while built on current .NET architecture: the .NET generic
host, EF Core, and a `Thing`/`Behavior` composition model instead of a
hardcoded class hierarchy.

!!! warning "Alpha — not yet stable"
    sharp-mud is pre-1.0: no SemVer compatibility guarantees between releases
    yet, and APIs/package boundaries/persisted-data shape can all still
    change. It's also currently built against a preview .NET 11 SDK. Use it,
    but expect breaking changes between versions.

sharp-mud ships as a set of NuGet packages you compose into your own game:

- **`SharpMud.Engine`** — `Thing`/`Behavior`, the event system, generic
  behaviors (rooms, exits, containment, identity), the command pipeline, and
  the global tick loop. Zero knowledge of any specific ruleset.
- **`SharpMud.Hosting`** — composition helpers for the .NET generic host:
  `WorldContext`, `IWorldBuilder`, `IPlayerFactory`, the session/login flow,
  and the `AddSharpMud*` extension methods that wire it all together.
- **`SharpMud.Persistence`** (+ **`.Sqlite`** / **`.DynamoDb`**) — EF Core
  repositories behind a provider-agnostic `IThingRepository`.
- **`SharpMud.Adapters.Cli`** / **`SharpMud.Adapters.Telnet`** — transport
  implementations of `ISession`.
- **`SharpMud.Ruleset.Rpg`** / **`SharpMud.Ruleset.Basic`** — optional
  reusable combat/encounter scaffolding, and a small runnable game built on
  it. See [Rulesets](rulesets.md).
- **`SharpMud`** — a meta-package pulling in `SharpMud.Engine`,
  `SharpMud.Hosting`, and `SharpMud.Persistence`. You always add a
  persistence provider and a transport explicitly — the meta-package
  doesn't include either (see [ADR-0007](https://github.com/LayeredCraft/sharp-mud/blob/main/docs/adr/0007-narrow-meta-package-scope.md)).

Your own game — stats, combat rules, world content, commands — is a separate
project that references these packages, either built on
`SharpMud.Ruleset.Rpg`'s combat scaffolding or entirely from scratch, the
same way `SharpMud.Samples.Classic` (the D&D-flavored reference sample in the
[sharp-mud repo](https://github.com/LayeredCraft/sharp-mud/tree/main/samples/SharpMud.Samples.Classic))
does.

The fastest way to a running game is [Rulesets](rulesets.md) — `dotnet add
package SharpMud.Ruleset.Basic` and a few lines gets you a playable game with
combat, no ruleset code of your own required. [Getting Started](getting-started.md)
instead walks through the bare engine with no ruleset at all, which is worth
reading if you want to understand the fundamentals before adopting a
ruleset package.
