# sharp-mud

A modern C#/.NET engine for building MUDs (Multi-User Dungeons) — faithful to
the genre's feel while built on current .NET architecture: the .NET generic
host, EF Core, and a `Thing`/`Behavior` composition model instead of a
hardcoded class hierarchy.

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
- **`SharpMud`** — a meta-package pulling in all of the above.

Your own game — stats, combat rules, world content, commands — is a separate
project that references these packages the same way `SharpMud.Samples.Classic`
(the D&D-flavored reference sample in the
[sharp-mud repo](https://github.com/LayeredCraft/sharp-mud/tree/main/samples/SharpMud.Samples.Classic))
does.

Start with [Getting Started](getting-started.md).
