namespace SharpMud.Engine.Core;

// See docs/persistence.md - one repository, not per-concept repositories,
// since Thing is the one entity type now (docs/engine-vs-ruleset.md).
public interface IThingRepository
{
    Task<Thing?> LoadTreeAsync(ThingId rootId, CancellationToken ct);
    Task SaveTreeAsync(Thing root, CancellationToken ct);

    // Matches PlayerBehavior.Username (the login identity), not Thing.Name
    // (the display name) - see docs/accounts-auth.md. They're set to the
    // same value at character creation today, but this is the semantically
    // correct field to search regardless.
    Task<Thing?> FindPlayerByUsernameAsync(string username, CancellationToken ct);
}
