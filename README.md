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
SharpMud.slnx
  src/
    SharpMud.Engine/                # Thing/Behavior, events, generic behaviors,
                                     # command pipeline, session, tick loop.
                                     # Zero deps on any ruleset.
    SharpMud.Hosting/                # generic-host composition helpers, ruleset-agnostic
    SharpMud.Persistence/            # EF Core repositories, provider-agnostic
    SharpMud.Persistence.Sqlite/     # SQLite provider
    SharpMud.Persistence.DynamoDb/   # DynamoDB provider
    SharpMud.Adapters.Cli/           # local stdin/stdout session adapter
    SharpMud.Adapters.Telnet/        # raw TCP session adapter + listener
    SharpMud/                        # meta-package (pulls in every SharpMud.* package)
  samples/
    SharpMud.Samples.Classic/        # D&D-flavored sample ruleset + composition root
  tests/
    SharpMud.Engine.Tests/
    SharpMud.Hosting.Tests/
    SharpMud.Persistence.Tests/
    SharpMud.Adapters.Cli.Tests/
    SharpMud.Adapters.Telnet.Tests/
    SharpMud.Samples.Classic.Tests/
```

## Building & testing

```
dotnet build SharpMud.slnx
dotnet test SharpMud.slnx
```

## Running

```
dotnet run --project samples/SharpMud.Samples.Classic       # local single-player CLI
dotnet run --project samples/SharpMud.Samples.Classic -- --telnet [port]  # telnet server, default port 4000
```

## Using SharpMud as a library

`src/` publishes as NuGet packages (`SharpMud.Engine`, `SharpMud.Hosting`,
`SharpMud.Persistence`(`.Sqlite`/`.DynamoDb`), `SharpMud.Adapters.Cli`/
`.Telnet`), plus a `SharpMud` meta-package pulling in all of them.
`samples/SharpMud.Samples.Classic` is a full reference consumer — start there
to see how a ruleset composes against the packages. See
[ADR-0006](docs/adr/0006-nuget-package-distribution.md) for the design.

## Containerized

```
docker build -t sharpmud .
docker run -p 4000:4000 sharpmud   # telnet server (container default)
```

See [docs/deployment.md](docs/deployment.md) for runtime configuration
(env vars, mode selection) and current limitations.
