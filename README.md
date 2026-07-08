# sharp-mud

A modern C#/.NET reimagining of a classic MUD (Multi-User Dungeon) — faithful
to the genre's feel while built with current .NET architecture.

- **`SPEC.md`** — vision and high-level decisions (start here).
- **`docs/`** — detailed per-subsystem design docs. Start with
  `docs/engine-vs-ruleset.md` for the entity model (`Thing`/`Behavior`
  composition), then architecture, world model, character, commands, combat,
  persistence, networking, accounts/auth.

## Solution layout

```
SharpMud.sln
  src/
    SharpMud.Engine/            # Thing/Behavior, events, generic behaviors,
                                 # command pipeline, session, tick loop.
                                 # Zero deps on any ruleset.
    SharpMud.Ruleset.Classic/   # D&D-flavored ruleset: stats, combat, kill/flee.
                                 # References Engine only.
    SharpMud.Persistence/       # EF Core repositories
    SharpMud.Adapters.Cli/      # local stdin/stdout session adapter
    SharpMud.Host/              # composition root / entry point / hub world content
  tests/
    SharpMud.Engine.Tests/
    SharpMud.Ruleset.Classic.Tests/
    SharpMud.Persistence.Tests/
```

## Building & testing

```
dotnet build SharpMud.sln
dotnet test SharpMud.sln
```
