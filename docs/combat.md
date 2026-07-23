# Combat

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [character.md](character.md) for Player stats and
[architecture.md](architecture.md) for the `ITickable`/`IGameLoop` mechanism
this system hooks into.

**Superseded by [engine-vs-ruleset.md](engine-vs-ruleset.md)**: everything on
this page now lives in `SharpMud.Ruleset.Rpg`, not `SharpMud.Engine` - combat
is ruleset-shaped, not ruleset-agnostic, by design (see the findings doc's
§9). `ICombatant` becomes `CombatantBehavior`, attached to whichever `Thing`s
the ruleset wants to be able to fight; `Player`/`Npc` references below mean "a
`Thing` with the relevant behaviors."

**Further split by [ADR-0008](adr/0008-ruleset-scaffolding-tier.md)**:
`CombatantBehavior`/`ICombatResolver`/`ICombatManager`/`AttackCommand`/
`FleeCommand` moved out of `SharpMud.Samples.Classic` into the packaged
`SharpMud.Ruleset.Rpg` tier, since none of that logic actually depended on
any Classic-specific type. The two touches that *did* need a concrete
ruleset - awarding XP into a stats behavior, and choosing a respawn room -
are no longer direct `CombatManager` code; they go through
`ICombatOutcomeHandler`, a per-ruleset hook (Classic's
`ClassicCombatOutcomeHandler`, Basic's `BasicCombatOutcomeHandler`)
registered alongside `AddSharpMudRpgRuleset<TCombatOutcomeHandler>(...)`.
`CombatManager` itself only knows "call the resolver, then call the outcome
handler" - it has zero reference to `StatsBehavior`, `Race`, or any concrete
world content.

## Model

Simple round-based combat (Diku/Circle-style), per SPEC.md: auto-attack on
the global tick, hit/miss/damage messages, minimal per-round input required
once engaged. **v1 scope is player-vs-NPC only** — no PvP verb or aggression
rules exist yet, so every encounter is keyed by the attacking `Thing`.

Implemented shape (`src/SharpMud.Ruleset.Rpg/`) differs from the original
sketch in two ways: `ITickable.OnTick` is `Task OnTickAsync(...)`, not `void`
(it needs to `await` `ISession` writes each round), and there's one
`CombatManager` registered with `IGameLoop`, not one `ITickable` per
encounter — it owns a `Dictionary<ThingId, CombatEncounter>` and resolves
every active encounter each tick:

```csharp
public sealed class CombatEncounter
{
    public required Thing Attacker { get; init; }
    public required Thing Defender { get; init; }
}

public interface ICombatManager
{
    bool IsInCombat(ThingId thingId);
    bool IsDefenderEngaged(ThingId defenderId);
    bool TryStartEncounter(Thing attacker, Thing defender);
    void EndEncounter(ThingId thingId);
    bool TryGetEncounter(ThingId thingId, [MaybeNullWhen(false)] out CombatEncounter encounter);
}

public sealed class CombatManager : ICombatManager, ITickable
{
    private readonly ICombatResolver _resolver;
    private readonly ICombatOutcomeHandler _outcomeHandler;

    public CombatManager(ICombatResolver resolver, ICombatOutcomeHandler outcomeHandler)
    {
        _resolver = resolver;
        _outcomeHandler = outcomeHandler;
    }

    public Task OnTickAsync(TickContext ctx, CancellationToken ct) { /* see below */ }
}
```

`_encounters` isn't only touched from the tick loop - `TryStartEncounter`/`EndEncounter`/etc. are also called from whichever session's command-execution task happens to be running `AttackCommand`/`FleeCommand` at that moment, and each connection runs independently (see `TelnetTransportBackgroundService`). `TryStartEncounter` checks "is this attacker already fighting" and "is this defender already engaged by someone else" and inserts the new encounter as one atomic operation (a `System.Threading.Lock` critical section) - not two separate steps - so two players targeting the same NPC at nearly the same time can't both succeed. `IsDefenderEngaged` alone is a point-in-time status query only, useful for a custom command that wants to display "who's fighting what," but not by itself race-free the way `TryStartEncounter` is.

No `hubRoomId`/`IWorld` constructor parameter — `CombatManager` has no
respawn-destination or world-lookup concept of its own. `ICombatOutcomeHandler`
(implemented per-ruleset) owns both the XP-award/death-penalty side effects
and the respawn destination:

```csharp
public interface ICombatOutcomeHandler
{
    Task OnVictoryAsync(Thing victor, Thing defeated, CancellationToken ct);
    Task<Thing> OnDefeatAsync(Thing defeated, Thing victor, CancellationToken ct);
}
```

Any `Thing` that can fight carries `CombatantBehavior` - a plain-data
`Behavior`, not an interface a domain type implements (see
[engine-vs-ruleset.md](engine-vs-ruleset.md) for why composition replaced
the original class-hierarchy sketch entirely):

```csharp
public sealed class CombatantBehavior : Behavior
{
    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    public int ArmorClass { get; set; }
    public int DamageMin { get; set; }
    public int DamageMax { get; set; }
    public int ExperienceReward { get; set; }
    public (int Min, int Max) DamageRange => (DamageMin, DamageMax);
}
```

Hit/damage formula: Diku/Circle-style d20-vs-AC roll — attacker rolls d20 vs.
defender Armor Class to hit; damage is a random roll within the attacker's
`DamageRange`. **Currently implemented as an unmodified d20 roll** — no
level/skill to-hit bonus yet (see Open Items; the modifier-scaling formula
is still undecided, so the code has nothing to apply).

Formulas live in a dedicated `ICombatResolver` (pure, unit-testable, no I/O)
implemented by `CombatResolver`, which both computes **and applies** the
round (mutates the defender's `CombatantBehavior.CurrentHitPoints` directly):

```csharp
public interface ICombatResolver
{
    CombatRoundResult ResolveRound(Thing attacker, Thing defender);
}

public sealed record CombatRoundResult(bool Hit, int Damage, bool DefenderDefeated);
```

## Sequence: Combat Round Resolves

1. Player types `"kill cave rat"` → `AttackCommand` matches the target among
   the current room's children carrying both `NpcBehavior` and
   `CombatantBehavior` (`ObjectMatcher.FindMatch`, case-insensitive), calls
   `ICombatManager.TryStartEncounter`, sends `"You attack cave rat!"`
   immediately if it returns `true` (engagement is instant; resolution is
   tick-gated). If the player is already in combat, the command sends `"You
   are already fighting!"` without calling `TryStartEncounter` at all; if the
   actor itself has no `CombatantBehavior` (a consumer's own `IPlayerFactory`
   forgot to attach it), it sends `"You have no way to fight."` instead of
   starting an encounter that would crash on the next tick. If
   `TryStartEncounter` itself returns `false` - the target is already
   engaged by a different attacker, checked and inserted atomically so two
   players targeting the same NPC at once can't both succeed - it sends
   `"Someone else is already fighting cave rat!"`.
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
path itself is covered by `SharpMud.Ruleset.Rpg.Tests`' `CombatManagerTests`
(`OnTickAsync_FreezesEncounter_WhenAttackerLinkdeadWithinGraceWindow`,
`OnTickAsync_AbandonsEncounter_WhenAttackerLinkdeadPastGraceWindow`), not a
separate live combat-specific manual test.

## Death & Respawn

Classic-stakes model, split between `CombatManager` (generic) and
`ICombatOutcomeHandler` (per-ruleset):

- **NPC death**: `CombatManager` sends the "You have slain ..." message and
  removes the NPC from the world, then calls `OnVictoryAsync` - Classic's
  handler increases `StatsBehavior.Experience` by
  `CombatantBehavior.ExperienceReward`; Basic's does the same against
  `BasicStatsBehavior`. Loot drops are **not implemented** — the item system
  itself is a later build-order phase, so there's nothing to drop yet.
- **Player death**: HP ownership is split between a generic safe baseline
  and a ruleset-specific override. `CombatManager` unconditionally resets
  the loser's `CombatantBehavior.CurrentHitPoints` to `MaxHitPoints` first
  (see the bug note below) — a "no penalty" ruleset can rely on that and do
  nothing further. `CombatManager` then calls `OnDefeatAsync`, which returns
  the respawn `Thing` and may apply a real death penalty: Classic's and
  Basic's handlers both reduce their own stats behavior's `Experience` by a
  flat **10%** (placeholder — exact percentage is still an open item), *and*
  halve `CombatantBehavior.CurrentHitPoints` again themselves (minimum 1) -
  since that's the value `CombatResolver` actually reads/writes, the earlier
  full-HP reset would otherwise make the documented "respawn at half HP"
  penalty a no-op. Classic additionally mirrors the halved value into
  `StatsBehavior.CurrentHitPoints` (its own character-sheet display field,
  never read by combat) so the two don't visibly drift. Respawn destination
  is `WorldContext.StartingRoom` for both (Classic's hub, Basic's clearing).
  `CombatManager` moves the attacker there and sends the room description
  via `LookCommand.SendRoomDescriptionAsync`. No item loss and no
  corpse-run.

  **Bug fixed during the ADR-0008 extraction**: `CombatResolver` reads/writes
  damage against `CombatantBehavior.CurrentHitPoints`, not any ruleset stats
  behavior. Respawn previously only reset the stats behavior's HP, leaving
  `CombatantBehavior.CurrentHitPoints` at/below 0 - the very next hit
  instantly re-triggered "defeated" regardless of the roll. `CombatManager`
  now resets `CombatantBehavior.CurrentHitPoints` itself, unconditionally,
  before the outcome handler runs — and a follow-up review round caught that
  the outcome handlers also needed to override that reset for their own
  penalty to have any actual combat effect (see above).

## Flee

Implemented in `FleeCommand` (`SharpMud.Ruleset.Rpg`). Requires an active
encounter (`ICombatManager.TryGetEncounter`) and at least one exit in the
current room. Success chance is currently a **flat 60%** via
`IDiceRoller.Roll(1, 100)` — the real DEX-differential formula from the
original design is still an open item, and neither `CombatantBehavior` nor
any built-in ruleset's stats behavior carries a Dexterity-equivalent value
yet, so there's nothing to differential against. On success, a random exit
is chosen (`IRandomSource.Next` over the room's exits — index selection
isn't dice notation, so it stays a direct `IRandomSource` call, not
`IDiceRoller`) and checked through the same `UseExitEvent` request path
`MoveCommand` uses (so a locked exit can still block a flee), the encounter
ends, and the actor moves exactly as a normal
move (see [commands.md](commands.md)).

## Dice-Rolling Abstraction

`IDiceRoller`/`DiceRoller` (`SharpMud.Ruleset.Rpg`) wraps `IRandomSource` with
"N dice of M sides plus a modifier" — `Roll(diceCount, sides, modifier)`.
`CombatResolver`'s to-hit roll and `FleeCommand`'s success check use it;
damage rolls stay direct `IRandomSource.Next(min, max)` calls, since a
damage range (e.g. 2-6) isn't 1-based dice notation and forcing it through
`IDiceRoller` would misrepresent the roll. DI-registered
(`AddSharpMudRpgRuleset(...)`), not a WheelMUD-style static singleton — see
[ADR-0008](adr/0008-ruleset-scaffolding-tier.md).

## Open Items

- Exact to-hit modifier scaling (how level/skill translate into a d20 bonus)
  — not implemented at all yet (currently an unmodified roll).
- ~~Real linkdead/reconnect handling~~ — resolved by ADR-0004, see
  Disconnect Mid-Fight above.
- Flee success-chance formula (exact DEX-differential-to-probability curve)
  — currently a flat 60%; would also need Dexterity added to
  `CombatantBehavior` or a separate ruleset-specific stats behavior lookup.
- XP-loss percentage on player death — currently a flat 10% placeholder.
- Respawn HP fraction — currently `MaxHitPoints / 2` placeholder.
- Loot drops on NPC death — not implemented; blocked on the item system.
- Tick interval default value (see [architecture.md](architecture.md)) is
  still the 2-second default from `GameLoopOptions`; not yet tuned against
  the placeholder combat formulas above.
