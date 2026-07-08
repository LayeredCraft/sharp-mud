namespace SharpMud.Engine.Core;

// Replaces the old RoomId/PlayerId/NpcId/ItemId/AreaId - a single Thing can
// play more than one role at once (see docs/engine-vs-ruleset.md), so
// per-role ID types stopped making sense. AccountId stays separate - an
// account is an auth identity outside the simulated world, not a Thing.
public readonly record struct ThingId(Guid Value)
{
    public static ThingId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
