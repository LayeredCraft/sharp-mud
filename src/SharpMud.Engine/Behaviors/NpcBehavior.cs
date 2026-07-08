using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

// Marker only - presence means "this Thing is an NPC." No combat stats;
// those live on a ruleset-specific behavior (e.g. CombatantBehavior in
// SharpMud.Ruleset.Classic).
public sealed class NpcBehavior : Behavior;
