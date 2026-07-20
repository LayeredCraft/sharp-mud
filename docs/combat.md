# Combat

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [character.md](character.md) for Player stats and
[architecture.md](architecture.md) for the `ITickable`/`IGameLoop` mechanism
this system hooks into.

**Superseded by [engine-vs-ruleset.md](engine-vs-ruleset.md)**: everything on
this page now lives in `SharpMud.Samples.Classic`, not `SharpMud.Engine` -
combat is ruleset-specific by design (see the findings doc's §9). `ICombatant`
becomes `CombatantBehavior`, attached to whichever `Thing`s the ruleset wants
to be able to fight; `Player`/`Npc` references below mean "a `Thing` with the
relevant behaviors."

## Model

Simple round-based combat (Diku/Circle-style), per SPEC.md: auto-attack on
the global tick, hit/miss/damage messages, minimal per-round input required
once engaged. **v1 scope is player-vs-NPC only** — no PvP verb or aggression
rules exist yet, so every encounter is keyed by the attacking player.

Implemented shape (`src/SharpMud.Engine/Combat/`) differs from the original
sketch in two ways: `ITickable.OnTick` is `Task OnTickAsync(...)`, not `void`
(it needs to `await` `ISession` writes each round — same reasoning as
`IWorld.MovePlayer` becoming `MovePlayerAsync`), and there's one
`CombatManager` registered with `IGameLoop`, not one `ITickable` per
encounter — it owns a `Dictionary<PlayerId, CombatEncounter>` and resolves
every active encounter each tick:

```csharp
public sealed class CombatEncounter
{
    public required Player Attacker { get; init; }
    public required Npc Defender { get; init; }
}

public interface ICombatManager
{
    bool IsInCombat(PlayerId playerId);
    void StartEncounter(Player attacker, Npc defender);
    void EndEncounter(PlayerId playerId);
    bool TryGetEncounter(PlayerId playerId, out CombatEncounter? encounter);
}

public sealed class CombatManager(IWorld world, ICombatResolver resolver, RoomId hubRoomId)
    : ICombatManager, ITickable
{
    public Task OnTickAsync(TickContext ctx, CancellationToken ct) { /* see below */ }
}
```

`ICombatant` also grew two members beyond the original sketch
(`CurrentHitPoints`/`ArmorClass`/`DamageRange` only) — `Name` and
`MaxHitPoints`, both needed for round messages and death/respawn handling
that the doc implied but didn't spell out as interface members:

```csharp
public interface ICombatant
{
    string Name { get; }
    int CurrentHitPoints { get; set; }
    int MaxHitPoints { get; }
    int ArmorClass { get; }
    (int Min, int Max) DamageRange { get; }
}
```

`Player` and `Npc` both implement `ICombatant`.

Hit/damage formula: Diku/Circle-style d20-vs-AC roll — attacker rolls d20 vs.
defender Armor Class to hit; damage is a random roll within the attacker's
`DamageRange`. **Currently implemented as an unmodified d20 roll** — no
level/skill to-hit bonus yet (see Open Items; the modifier-scaling formula
is still undecided, so the code has nothing to apply).

Formulas live in a dedicated `ICombatResolver` (pure, unit-testable, no I/O):

```csharp
public interface ICombatResolver
{
    CombatRoundResult ResolveRound(ICombatant attacker, ICombatant defender);
}

public sealed record CombatRoundResult(bool Hit, int Damage, bool DefenderDefeated);
```

`ResolveRound` both computes **and applies** the round (mutates
`defender.CurrentHitPoints` directly) — matching the original "resolve one
round: hit check → damage → apply → check death" description.

## Sequence: Combat Round Resolves

1. Player types `"kill cave rat"` → `AttackCommand` resolves the target NPC
   in the room (`ctx.CurrentRoom.Npcs` → `IWorld.GetNpc`, matched by
   case-insensitive substring), calls `ICombatManager.StartEncounter`, sends
   `"You attack cave rat!"` immediately (engagement is instant; resolution is
   tick-gated). If the player is already in combat, the command instead sends
   `"You are already fighting!"`.
2. On the next global tick, `IGameLoop` calls `CombatManager.OnTickAsync`,
   which iterates every active encounter.
3. `ICombatResolver.ResolveRound(attacker, defender)` computes the player's
   attack; a hit/miss message is sent immediately via the player's session.
4. If the NPC is defeated, see Death & Respawn below and the round ends there
   (no counter-attack).
5. Otherwise, **the NPC counter-attacks the same round** (classic mutual
   combat) via a second `ResolveRound(defender, attacker)` call; another
   hit/miss message is sent. If this defeats the player, see Death & Respawn.
6. Otherwise the encounter stays in the dictionary and resolves again next
   tick — repeats without further player input until death or `flee`
   (disconnect handling is a stub for now, see below).

## Disconnect Mid-Fight ✅ (ADR-0004)

Each tick, `CombatManager` checks the attacker's `PlayerBehavior
.ConnectionState` (see [networking.md](networking.md)'s Reconnect / Session
Resumption for the full `Linkdead` mechanism):

- `Linkdead` and still within `ReconnectPolicy.GraceWindow` of
  `LinkdeadSinceUtc` — the encounter **freezes**: no round resolves, no
  session writes happen, nothing is removed. It resumes automatically once
  `LoginFlow` reconnects the player (`ConnectionState` flips back to
  `Playing`) — no combat-specific reconnect logic needed, `CombatManager`
  just sees a `Playing` attacker again on the next tick.
- `Linkdead` and past `ReconnectPolicy.GraceWindow` — the encounter is
  abandoned (`EndEncounter`), same outcome as the old stub, just delayed
  until the grace window genuinely expires instead of ending the instant
  the connection dropped.

Verified live over real Telnet as part of ADR-0004/PLAN-0004 (see
[networking.md](networking.md)) that a disconnect mid-session leaves the
character (and by extension any encounter) resumable; the encounter-freeze
path itself is covered by `CombatManagerTests`
(`OnTickAsync_FreezesEncounter_WhenAttackerLinkdeadWithinGraceWindow`,
`OnTickAsync_AbandonsEncounter_WhenAttackerLinkdeadPastGraceWindow`), not a
separate live combat-specific manual test.

## Death & Respawn

Classic-stakes model, implemented in `CombatManager`:

- **NPC death**: attacker's `Player.Experience` increases by
  `Npc.ExperienceReward`, the NPC is removed from the world via
  `IWorld.RemoveNpc` (which also removes it from its room's occupant list —
  see [world-model.md](world-model.md)), the encounter ends. Loot drops are
  **not implemented** — the item system itself is a later build-order phase,
  so there's nothing to drop yet.
- **Player death**: `Player.Experience` is reduced by a flat **10%**
  (placeholder — exact percentage is still an open item), `CurrentHitPoints`
  is reset to `MaxHitPoints / 2` (placeholder — exact fraction is still an
  open item, minimum 1), `CurrentRoomId` is reset to the hub room, and the
  hub's description is sent via `LookCommand.SendRoomDescriptionAsync`. No
  item loss and no corpse-run.

## Flee

Implemented in `FleeCommand`. Requires an active encounter
(`ICombatManager.TryGetEncounter`) and at least one exit in the current room.
Success chance is currently a **flat 60%** via `IRandomSource.Next(1, 100)`
— the real DEX-differential formula from the original design is still an
open item, and `Npc`/`ICombatant` doesn't carry Dexterity, so there's nothing
to differential against yet. On success, a random exit is chosen
(`IRandomSource.Next` over `Room.Exits`), the encounter ends, and
`IWorld.MovePlayerAsync` runs exactly as a normal move (see
[commands.md](commands.md)).

## Open Items

- Exact to-hit modifier scaling (how level/skill translate into a d20 bonus)
  — not implemented at all yet (currently an unmodified roll).
- ~~Real linkdead/reconnect handling~~ — resolved by ADR-0004, see
  Disconnect Mid-Fight above.
- Flee success-chance formula (exact DEX-differential-to-probability curve)
  — currently a flat 60%; would also need Dexterity added to `ICombatant` or
  a separate lookup.
- XP-loss percentage on player death — currently a flat 10% placeholder.
- Respawn HP fraction — currently `MaxHitPoints / 2` placeholder.
- Loot drops on NPC death — not implemented; blocked on the item system.
- Tick interval default value (see [architecture.md](architecture.md)) is
  still the 2-second default from `GameLoopOptions`; not yet tuned against
  the placeholder combat formulas above.
