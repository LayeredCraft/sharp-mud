namespace SharpMud.Engine.Core;

// See docs/persistence.md - one repository, not per-concept repositories,
// since Thing is the one entity type now (docs/engine-vs-ruleset.md).
public interface IThingRepository
{
    Task<Thing?> LoadTreeAsync(ThingId rootId, CancellationToken ct);
    Task SaveTreeAsync(Thing root, CancellationToken ct);
    Task<Thing?> FindPlayerByNameAsync(string name, CancellationToken ct);
}
