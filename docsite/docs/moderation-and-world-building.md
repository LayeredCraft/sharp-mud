# Moderation & World Building

`SharpMud.Engine` ships two optional, ready-made command sets on top of the
same underlying mechanism: a security-role system that gates who can run
what. Neither is registered for you automatically — you opt into each one
explicitly, the same way you'd register your own `ICommand`s (see
[Customizing sharp-mud](customizing.md#adding-your-own-command)).

- **Moderation** (`boot`, `mute`/`unmute`, `announce`, `ban`/`unban`,
  `rolegrant`/`rolerevoke`) — day-to-day admin tooling for a live server.
- **World building** (`dig`, `tunnel`, `describe`) — lets a trusted player
  extend the world from inside the game, no redeploy required.

## `SecurityRole`: the mechanism both sets are built on

`SecurityRole` is a `[Flags]` enum on `PlayerBehavior.Roles` — a player can
hold more than one role at once. Every new character starts as `Player`.
The two tiers relevant here:

| Tier | Implies | Unlocks |
|---|---|---|
| `MinorAdmin` | `Player` | `boot`, `mute`, `unmute`, `announce` |
| `FullAdmin` | `MinorAdmin`, `Player` | everything `MinorAdmin` can do, plus `ban`, `unban`, `rolegrant`, `rolerevoke` |
| `MinorBuilder` | — | `dig`, `tunnel`, `describe` |
| `FullBuilder` | `MinorBuilder` | (reserved for a future builder-tier command; no consumer yet) |

"Implies" is real: granting `FullAdmin` also grants `MinorAdmin` and
`Player` in the same call, so a `FullAdmin` can immediately run every
`MinorAdmin` command too — you never have to grant both separately.

Commands aren't gated by a guard clause you have to remember to write.
`ICommandRegistry` only exposes two ways to register a command:

```csharp
void RegisterOpen(ICommand command);                              // anyone can run it
void RegisterWithRole(ICommand command, SecurityRole requiredRole); // gated
```

`RegisterWithRole` wraps your command in a decorator that checks the
actor's `Roles` before it ever runs — there's no third, silent way to
register something without declaring its access level.

### Bootstrapping the first admin

Since granting a role itself requires already holding `FullAdmin`, nothing
in-game can produce the *first* one. Set the `SHARPMUD_INITIAL_ADMIN`
environment variable to a username, and that character is granted
`FullAdmin` automatically the moment they log in — whether that's a brand
-new character or an existing one, on this boot or any future one. Wire it
up once in your composition root:

```csharp
var env = new Dictionary<string, string?>
{
    ["SHARPMUD_INITIAL_ADMIN"] = Environment.GetEnvironmentVariable("SHARPMUD_INITIAL_ADMIN"),
};
var hostOptions = SharpMudHostOptions.Parse(env);
app.Services.AddSingleton(hostOptions);
```

It's safe to leave the variable set permanently — granting an already-held
role is a no-op, not an error.

## Registering the command sets

Both command sets need `IThingRepository` (most of their commands look up
an offline target, or save a newly-created room). Resolve it once inside
whichever callback you already pass into `AddSharpMudRuleset`/
`AddSharpMudRpgRuleset` and register both from there:

```csharp
app.Services.AddSharpMudRpgRuleset<MyCombatOutcomeHandler>((sp, registry) =>
{
    var repository = sp.GetRequiredService<IThingRepository>();
    AdminCommands.RegisterAll(registry, repository);
    BuilderCommands.RegisterAll(registry, repository);
});
```

!!! warning "One registration callback, not several"
    `AddSharpMudRuleset`/`AddSharpMudRpgRuleset` only calls your command
    -registration callback once. Calling either method a second time
    replaces the first registration instead of adding to it — always
    register everything (built-ins are automatic; admin, builder, and
    your own commands) from inside the one callback.

### Moderation commands

| Command | Role | Notes |
|---|---|---|
| `boot <player>` | `MinorAdmin` | Disconnects a currently-online player. |
| `mute` / `unmute <player>` | `MinorAdmin` | Blocks/restores `say`/`emote` for that player. |
| `announce <message>` | `MinorAdmin` | Broadcasts to every connected player. |
| `ban` / `unban <player>` | `FullAdmin` | Blocks/restores login entirely; an already-connected banned player is also disconnected immediately. |
| `rolegrant` / `rolerevoke <player> <role>` | `FullAdmin` | Grants/revokes a role. `all`/`none` are rejected — those are sentinel values, not real assignable tiers. |

### World-building commands

| Command | Role | Notes |
|---|---|---|
| `dig <direction> <name>` | `MinorBuilder` | Creates a brand-new room and wires a two-way exit from the builder's current room to it. |
| `tunnel <direction> <existing room name>` | `MinorBuilder` | Wires a two-way exit between the current room and an *already-existing* room, found by exact name. |
| `describe <text>` | `MinorBuilder` | Sets the current room's description. |

Both `dig` and `tunnel` reject the direction if the current room already
has an exit that way (and, for `tunnel`, if the destination room already
has an exit going back the opposite way) — you'll never end up with two
exits silently fighting over the same direction.

There's no room-deletion command, and no NPC/item spawning — see
[ADR-0009](https://github.com/LayeredCraft/sharp-mud/blob/main/docs/adr/0009-world-building-olc-command-surface.md)
for what's deliberately out of scope.

## Further reading

The full design rationale for both command sets — why a hand-rolled
decorator instead of an attribute+reflection scheme, why `SecurityRole`
adopts WheelMUD's full role set even though most of it has no consumer
yet, and why `dig`/`tunnel` look up rooms by name instead of a numeric id
— is recorded in the sharp-mud repo's ADRs:

- [ADR-0005](https://github.com/LayeredCraft/sharp-mud/blob/main/docs/adr/0005-security-role-model-and-moderation-commands.md) — Security Role Model + Moderation Commands
- [ADR-0009](https://github.com/LayeredCraft/sharp-mud/blob/main/docs/adr/0009-world-building-olc-command-surface.md) — World-Building/OLC Command Surface
