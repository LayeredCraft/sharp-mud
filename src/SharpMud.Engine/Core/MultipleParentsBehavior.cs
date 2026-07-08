namespace SharpMud.Engine.Core;

// Generic engine primitive for a Thing that needs to live in more than one
// container at once (docs/research/wheelmud-findings.md §6). Not currently
// used by anything in sharp-mud's own content - exits are one-Thing-per-
// direction instead (see docs/engine-vs-ruleset.md Decisions) - but kept
// available for a ruleset that genuinely needs it.
public sealed class MultipleParentsBehavior : Behavior
{
    public void AddParent(Thing parent) => Parent!.AddSecondaryParent(parent);

    public void RemoveParent(Thing parent) => Parent!.RemoveSecondaryParent(parent);
}
