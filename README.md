# sharp-mud

A modern C#/.NET reimagining of a classic MUD (Multi-User Dungeon) — faithful
to the genre's feel while built with current .NET architecture.

- **`SPEC.md`** — vision and high-level decisions (start here).
- **`docs/`** — detailed per-subsystem design docs (architecture, world model,
  character, commands, combat, persistence, networking, accounts/auth).

## Solution layout

```
SharpMud.sln
  src/
    SharpMud.Engine/            # domain + game loop, zero external deps
    SharpMud.Persistence/       # EF Core repositories
    SharpMud.Adapters.Cli/      # local stdin/stdout session adapter
    SharpMud.Host/              # composition root / entry point
  tests/
    SharpMud.Engine.Tests/
    SharpMud.Persistence.Tests/
```

## Building & testing

```
dotnet build SharpMud.sln
dotnet test SharpMud.sln
```
