# Combat

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [character.md](character.md) for Player stats and
[architecture.md](architecture.md) for the `ITickable`/`IGameLoop` mechanism
this system hooks into.

## Model

Simple round-based combat (Diku/Circle-style), per SPEC.md: auto-attack on
the global tick, hit/miss/damage messages, minimal per-round input required
once engaged.

```csharp
public sealed class CombatEncounter : ITickable
{
    public required Player Attacker { get; init; }
    public required ICombatant Defender { get; init; } // Player or Npc
    public CombatState State { get; set; }

    public void OnTick(TickContext ctx)
    {
        // resolve one round: hit check -> damage -> apply -> check death
    }
}

public interface ICombatant
{
    int CurrentHitPoints { get; set; }
    int ArmorClass { get; }
    (int min, int max) DamageRange { get; }
}

public enum CombatState { Engaged, Fleeing, Linkdead, Abandoned, Ended }
```

Hit/damage formula: Diku/Circle-style THAC0-ish roll — attacker rolls d20 +
level/skill modifiers vs. defender Armor Class to hit; damage = weapon dice +
STR modifier (see [character.md](character.md) for the attribute source).
Directly authentic to the classic MUD lineage this project revives; exact
modifier scaling (how level/skill translate to a to-hit bonus) still to be
tuned, see Open Items.

Formulas live in a dedicated `ICombatResolver` (pure, unit-testable, no I/O)
so combat math can be tested without a live tick loop or session:

```csharp
public interface ICombatResolver
{
    CombatRoundResult ResolveRound(ICombatant attacker, ICombatant defender);
}

public sealed record CombatRoundResult(bool Hit, int Damage, bool DefenderDefeated);
```

## Sequence: Combat Round Resolves

1. Player types `"kill goblin"` → `AttackCommand` resolves the target NPC in
   the room, creates a `CombatEncounter`, registers it with `IGameLoop` as an
   `ITickable`, sends `"You attack the goblin!"` immediately (engagement is
   instant; resolution is tick-gated).
2. On the next global tick, `IGameLoop` calls `CombatEncounter.OnTick`.
3. `ICombatResolver` computes hit/miss (e.g. attacker roll vs. defender AC),
   then damage if hit.
4. `CombatEncounter` applies damage to `Defender.CurrentHitPoints`, sends
   round message to both combatants' rooms via `ISession`.
5. If `CurrentHitPoints <= 0`: encounter ends, death/loot handling fires,
   encounter deregisters from `IGameLoop`.
6. Otherwise the encounter stays registered and resolves again next tick —
   repeats without further player input until death, flee, or disconnect.

## Sequence: Player Disconnects Mid-Fight

1. The transport adapter detects the underlying stream closed/EOF, calls
   `ISession.DisconnectAsync` (see [networking.md](networking.md)).
2. `Host`'s session-loop catches this, fires a `PlayerDisconnectedEvent`.
3. Engine's disconnect handler: if the player has an active
   `CombatEncounter`, the encounter transitions to `CombatState.Linkdead`. The
   NPC keeps attacking the disconnected player's body for a grace period
   (a fixed number of ticks — classic MUD tension/risk: you can die while
   disconnected). If the player doesn't reconnect before the grace period
   expires, the encounter force-ends as `CombatState.Abandoned` and final
   state is saved. If they reconnect in time, the encounter resumes normally.
   Exact grace-period tick count is an Open Item.
4. `IPlayerRepository.SaveAsync` persists final state regardless of combat
   outcome so no progress is lost on disconnect.
5. Room occupants notified ("Alice's link has died.").

## Death & Respawn

Classic-stakes model:

- **NPC death**: `CurrentHitPoints <= 0` → items drop to `Room.ItemsOnGround`
  (see [world-model.md](world-model.md)), XP awarded to the attacker (applied
  to `Player.Experience`, see [character.md](character.md)), NPC removed from
  the room.
- **Player death**: XP-loss penalty — player loses a percentage of
  current-level XP (exact percentage TBD, see Open Items), respawns at the
  hub/starting area (`Player.CurrentRoomId` reset to the hub `RoomId`) with
  HP reset to a fraction of `MaxHitPoints` (exact fraction TBD). No item loss
  and no corpse-run — items stay in inventory/equipped through death,
  keeping the death loop low-friction (no corpse decay/looting system
  needed).

## Flee

`flee` command attempts escape to a random adjacent room (via `Room.Exits`,
see [world-model.md](world-model.md)). Success chance scales with the
fleeing combatant's Dexterity vs. the opponent's (exact formula TBD, see
Open Items). On failure, the round is wasted (no movement, no attack) and
the combatant remains `CombatState.Engaged`. On success, `MovePlayer` runs
as in a normal move (see [commands.md](commands.md)) and the
`CombatEncounter` ends.

## Open Items

- Exact to-hit modifier scaling (how level/skill translate into a d20 bonus)
  — formula style decided above, precise numbers not yet tuned.
- Grace-period tick count before a linkdead encounter is force-abandoned —
  mechanism decided above, exact value not yet chosen.
- Flee success-chance formula (exact DEX-differential-to-probability curve)
  — mechanism decided above, precise numbers not yet tuned.
- XP-loss percentage on player death — mechanism decided above, exact
  percentage not yet chosen.
- Respawn HP fraction (full heal vs. partial) not yet chosen.
- Tick interval default value (see [architecture.md](architecture.md), now
  configurable rather than hardcoded) directly affects combat pacing feel —
  needs to be tuned alongside the damage formula, not independently.
