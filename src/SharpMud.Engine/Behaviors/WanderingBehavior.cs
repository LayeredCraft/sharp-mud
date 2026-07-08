using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

// Presence + WanderManager (registered once with IGameLoop, like
// CombatManager in SharpMud.Ruleset.Classic) means "this NPC randomly moves
// to an adjacent room some percentage of ticks." Engine-level (not
// ruleset-specific) since wandering doesn't depend on any combat/stat system.
public sealed class WanderingBehavior : Behavior
{
    public int WanderChancePercent { get; set; } = 25;
}
