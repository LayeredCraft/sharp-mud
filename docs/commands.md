# Commands

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [world-model.md](world-model.md) for `IWorld`/`Room`, and
[networking.md](networking.md) for `ISession`.

**Updated by [engine-vs-ruleset.md](engine-vs-ruleset.md)**: `CommandContext`
now carries `Thing` instead of `Player`/`Room`; the object-matching/error-
handling/verb-list content below is unchanged.

## Pipeline

```csharp
public interface ICommand
{
    string Verb { get; }                 // canonical form, e.g. "look"
    IReadOnlyList<string> Aliases { get; } // e.g. ["l"]
    Task ExecuteAsync(CommandContext ctx, CancellationToken ct);
}

public sealed record CommandContext(
    Player Actor,
    Room CurrentRoom,
    IReadOnlyList<string> Args,
    IWorld World,
    ISession Session);

public interface ICommandParser
{
    ParsedCommand Parse(string rawInput);
}

public sealed record ParsedCommand(
    string Verb,
    IReadOnlyList<string> Args,
    string RawInput);

public interface ICommandRegistry
{
    bool TryResolve(string verb, out ICommand command);
    void Register(ICommand command);
}
```

Classic verb-first commands (`look`, `north`/`n`, `get sword`, `kill goblin`)
with standard MUD abbreviations and directional shortcuts, plus
player-defined aliases (stored on `Player.Aliases`, see
[character.md](character.md)).

Aliases resolve to the same `ICommand` as their canonical verb; ambiguous
abbreviations (e.g. `"n"` matching both `north` and a custom alias) resolve
deterministically — built-in directions take priority over user aliases.

Soft-code/in-game scripting for builders is explicitly **deferred** per
SPEC.md — v1 NPC/room behavior is data/config-driven only, no embedded
scripting language yet.

## Error Handling

Decision: terse in-character to the player, verbose in a debug/dev channel.

- Unresolvable verb → player sees `"Huh?"` via `ISession`; a structured
  `CommandNotFoundEvent { RawInput, SessionId, Timestamp }` goes to
  `ILogger` at Debug level.
- Invalid direction/target → in-character message (`"You can't go that
  way."`, `"You don't see that here."`); same dual-channel pattern.

## Sequence: Player Types "north"

1. The transport adapter (see [networking.md](networking.md)) reads a line
   via `ISession`, passes it to the per-session loop in `Host`.
2. `ICommandParser.Parse("north")` → `ParsedCommand(Verb: "north", Args: [])`.
3. `ICommandRegistry.TryResolve("north", ...)` — direction verbs are
   registered as `ICommand` implementations at startup, so `"north"` resolves
   to a `MoveCommand` pre-bound to `Direction.North` (same for the `"n"`
   alias).
4. `MoveCommand.ExecuteAsync(ctx, ct)`:
   - Looks up `ctx.CurrentRoom.Exits` for `Direction.North`.
   - Not found → `ctx.Session.WriteLineAsync("You can't go that way.")`, log
     `CommandFailedEvent` at Debug, return.
   - Found but `Lock.IsLocked == true` → `"The door is locked."`, return.
   - Found and passable → `ctx.World.MovePlayer(ctx.Actor, ctx.CurrentRoom,
     destinationRoom)` (see [world-model.md](world-model.md); if the
     destination is ungenerated frontier space, generation happens here
     first).
5. `IWorld.MovePlayer`:
   - Updates `Player.CurrentRoomId`.
   - Notifies old room's other occupants ("Alice leaves north.") and new
     room's occupants ("Alice arrives.") via their `ISession`s.
   - Triggers `IPlayerRepository.SaveAsync` (position persisted — v1:
     persist immediately on every move for simplicity; revisit if write
     volume matters — see [persistence.md](persistence.md)).
6. New room description auto-sent to the moving player (equivalent to an
   implicit `look`).

## Object Matching

Classic MUD ordinal syntax, prefix-dot notation: `get sword` matches the
first/nearest match in scope (room, then inventory, depending on verb);
`get 2.sword` selects the second match, etc. The ordinal token always sits
immediately before the object name, so there's no ambiguity with item names
that happen to contain numbers (unlike a trailing-number scheme). Applied
consistently across all object-targeting verbs (`get`, `drop`, `kill`,
`wear`, `give`, etc) via a single shared `ObjectMatcher.FindMatch<T>` helper
(`src/SharpMud.Engine/Commands/ObjectMatcher.cs`) rather than each command
reimplementing the parsing.

## V1 Verb List

Broader set — core interaction plus social/utility. Status reflects what's
actually implemented as of the inventory/items build-order phase:

- **Movement** ✅: `north`/`n`, `south`/`s`, `east`/`e`, `west`/`w`,
  `northeast`/`ne`, `northwest`/`nw`, `southeast`/`se`, `southwest`/`sw`,
  `up`/`u`, `down`/`d`.
- **Perception**: `look`/`l` ✅ (room only so far — `look <target>` and
  `examine`/`ex` for a single object/NPC are **not implemented yet**).
- **Social**: `say` ✅, `emote`/`:` ✅, `who` ✅. `tell <player> <msg>`
  (direct/private message) is **not implemented yet**.
- **Items** ✅: `inventory`/`i`, `get`/`take`, `drop`, `wear`, `remove`,
  `give <item> to <player>`.
- **Combat** ✅: `kill`/`attack`, `flee` (see [combat.md](combat.md) for
  placeholder formulas still pending).
- **Character**: `score`/`stats` (display derived stats — see
  [character.md](character.md)) is **not implemented yet**.
- **Meta** ✅: `help`, `quit`.
- **Builder/OLC verbs** (`@dig`, `@describe`, etc.) explicitly excluded —
  those belong to the deferred in-game building phase (see
  [world-model.md](world-model.md)). **Moderation/admin verbs** ✅
  (`boot`/`mute`/`unmute`/`announce`/`ban`/`unban`/`rolegrant`/
  `rolerevoke`) are role-gated via `ICommandRegistry.RegisterWithRole` —
  see [ADR-0005](adr/0005-security-role-model-and-moderation-commands.md)
  and [PLAN-0005](plans/0005-security-role-model-and-moderation-commands.md).

## Open Items

- `help` command content/structure (static text vs. per-verb generated docs)
  not yet designed.
- `who` list formatting and whether it shows location/level or just names.
