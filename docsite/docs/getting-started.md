# Getting Started

This walks through the smallest possible sharp-mud game: one room, one
built-in command set, no custom ruleset commands yet. It uses the CLI
transport and SQLite persistence — the fastest loop for local iteration.

For a full worked example with stats, combat, and a Telnet transport, see
[`SharpMud.Samples.Classic`](https://github.com/LayeredCraft/sharp-mud/tree/main/samples/SharpMud.Samples.Classic)
in the sharp-mud repo.

## Install

```bash
dotnet add package SharpMud
dotnet add package SharpMud.Persistence.Sqlite
dotnet add package SharpMud.Adapters.Cli
```

`SharpMud` is a meta-package pulling in `SharpMud.Engine`, `SharpMud.Hosting`,
`SharpMud.Persistence`, and both adapter packages — you still pick a
persistence *provider* (`.Sqlite` or `.DynamoDb`) and a transport
(`SharpMud.Adapters.Cli` and/or `SharpMud.Adapters.Telnet`) explicitly.

## Build the world

Every consumer implements `IWorldBuilder` to describe what a brand-new world
looks like, and `IPlayerFactory` to describe how a new character is created:

```csharp
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Hosting;

public sealed class MyWorldBuilder : IWorldBuilder
{
    public static readonly ThingId RootRoomId = ThingId.New();

    public ThingId RootId => RootRoomId;

    public (World World, Thing StartingRoom) Build()
    {
        var world = new World();

        var room = new Thing { Id = RootRoomId, Name = "The Square", Description = "A quiet town square." };
        room.Behaviors.Add(new RoomBehavior());
        world.Register(room);

        return (world, room);
    }

    public Thing FindStartingRoom(Thing root) => root;
}

public sealed class MyPlayerFactory : IPlayerFactory
{
    public Thing CreatePlayer(World world, string username, string passwordHash, Thing startingRoom)
    {
        var player = new Thing { Id = ThingId.New(), Name = username };
        player.Behaviors.Add(new PlayerBehavior { Username = username, PasswordHash = passwordHash });
        startingRoom.Add(player);
        world.Register(player);
        return player;
    }
}
```

## Compose the host

```csharp
using Microsoft.Extensions.Hosting;
using SharpMud.Adapters.Cli;
using SharpMud.Hosting;
using SharpMud.Persistence.Sqlite;

var app = SharpMudApplication.CreateBuilder(args);

app.Services.AddSharpMudSqlitePersistence("./mygame.db");
app.Services.AddSharpMudWorld<MyWorldBuilder>();
app.Services.AddSharpMudPlayerFactory<MyPlayerFactory>();
app.Services.AddSharpMudRuleset((sp, registry) => { /* register your own ICommand types here */ });
app.Services.AddSharpMudCliTransport();

var mud = app.Build();
await mud.RunAsync();
```

`SharpMudApplication.CreateBuilder` wraps the .NET generic host
(`Host.CreateApplicationBuilder`), so `app` is a normal
`IHostApplicationBuilder` — standard `appsettings.json`/environment
configuration, logging, and DI all work exactly as they do in any other
.NET generic-host app.

## Run it

```bash
dotnet run
```

The CLI transport is login-free (single local player, resolved/created as
`"Adventurer"` on start) — you land straight in `The Square`. Built-in
commands (`look`, movement, `quit`, etc.) work immediately, registered
automatically by `AddSharpMudRuleset` before your own callback runs.
`AddSharpMudTelnetTransport(port)` is the transport that uses
`SharpMud.Hosting`'s username/password login flow, since a networked
multi-player server needs one.

To add a real ruleset — stats, combat, more rooms — see
`SharpMud.Samples.Classic`'s `Program.cs` and `HubWorldBuilder` for a fuller
worked example, and [ADR-0006](https://github.com/LayeredCraft/sharp-mud/blob/main/docs/adr/0006-nuget-package-distribution.md)
for the design rationale behind this package split.
