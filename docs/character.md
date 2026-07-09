# Character (Player)

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [combat.md](combat.md) for how these stats feed combat,
and [persistence.md](persistence.md) for how a Player is stored.

**Superseded by [engine-vs-ruleset.md](engine-vs-ruleset.md)**: `Player` below
is no longer a dedicated class — it's a `Thing` composed from an engine-level
`PlayerBehavior` (identity only) plus a `SharpMud.Ruleset.Classic`
`StatsBehavior` (everything on this page). This doc still describes the
correct stat shape; engine-vs-ruleset.md describes which project owns which
piece and why.

## Stat System

Decision: classic D&D-style attributes **combined with** Race/Class-driven
modifiers (not one or the other) — base attributes exist on every character,
and the chosen Race + Class apply modifiers on top.

```csharp
public sealed class Player
{
    public PlayerId Id { get; init; }
    public string Username { get; init; } = ""; // see accounts-auth.md (revised from AccountId/OAuth)
    public string Name { get; set; } = "";

    public Race Race { get; set; }
    public CharacterClass Class { get; set; }

    // Base attributes; Race/Class apply modifiers on top of these
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Charisma { get; set; }

    // Derived (computed from attributes + Race + Class + Level; Max* values
    // are recalculated on level/stat change, Current* are persisted directly
    // since they change moment to moment during play)
    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    public int MaxMana { get; set; }
    public int CurrentMana { get; set; }
    public int MaxStamina { get; set; }
    public int CurrentStamina { get; set; }

    public int Level { get; set; }
    public long Experience { get; set; }

    public RoomId CurrentRoomId { get; set; }

    // Item instances live in IWorld (like NPCs/rooms) - Player only holds
    // ItemId references, resolved via IWorld.GetItem. Same pattern as
    // Room.ItemsOnGround / Room.Npcs (see world-model.md).
    public List<ItemId> Inventory { get; set; } = [];
    public Dictionary<EquipSlot, ItemId?> Equipped { get; set; } = [];

    public List<string> Aliases { get; set; } = []; // player-defined command macros
}

public enum Race { Human, Elf, Dwarf, Halfling /* extend later */ }
public enum CharacterClass { Warrior, Mage, Cleric, Rogue /* extend later */ }
```

`Race` and `CharacterClass` are modifier providers (e.g. `Dwarf` → +CON,
`Mage` → higher Mana-per-Intelligence multiplier) — implemented as a lookup
table (`IReadOnlyDictionary<Race, StatModifiers>`,
`IReadOnlyDictionary<CharacterClass, StatModifiers>`), not
inheritance/subclassing, so new races/classes are data additions, not new
types.

## Login Identity (revised — was Account Relationship)

No separate `Account` entity, no multi-character "alts" — one character per
login. `Username` + a hashed password live directly on the player `Thing`
(via `PlayerBehavior`, see [engine-vs-ruleset.md](engine-vs-ruleset.md)).
See [accounts-auth.md](accounts-auth.md) for the full login flow and the
rationale for this simplification.

## Character Creation

Classic dice-roll: attributes rolled via 4d6-drop-lowest (or similar) at
character creation, per attribute, with Race/Class modifiers (see Stat System
above) applied on top of the rolled base values. Authentic old-school
MUD/D&D ritual — includes the option to reroll until the player is happy with
the array, matching genre convention. Less predictable/balanced than a fixed
array, which is an accepted tradeoff for nostalgia.

## Derived Stat Formulas

Classic Diku-style scaling —
`MaxHitPoints = BaseHp + Level × (ConstitutionModifier + ClassHitDieAverage)`;
`MaxMana`/`MaxStamina` similarly keyed off Intelligence/Wisdom and
Constitution respectively, with class-specific multipliers (see
[combat.md](combat.md) for how these feed `ICombatant`).

## Open Items

- Exact reroll rules for dice-roll character creation (unlimited rerolls vs.
  capped, and whether Race/Class selection happens before or after rolling).
- Actual per-class hit-die/mana/stamina constants for the derived-stat
  formulas — shape of the formula is decided, numbers are not.
- Actual stat modifier tables for each Race and each Class — mechanism is
  defined in the Stat System section above, numbers are not. `Race`/
  `CharacterClass` are now `LayeredCraft.OptimizedEnums` (source-generated
  smart enums, not plain `enum`s) specifically so each singleton instance can
  eventually carry its own modifier data directly, rather than a parallel
  `Dictionary<Race, StatModifiers>` lookup table.
