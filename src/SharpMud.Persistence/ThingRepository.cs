using Microsoft.EntityFrameworkCore;
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Persistence;

// v1 scope: reconstructs the entire stored world into memory per call,
// rather than a properly scoped/paginated graph load - correct and simple
// for a hand-built hub plus a modest number of players, not yet suitable for
// a large procedurally generated world. See docs/persistence.md Open Items.
//
// Uses a fresh GameDbContext per call (IDbContextFactory), not a shared
// instance - required both because DbContext isn't thread-safe (multiple
// Telnet sessions can call this concurrently) and because re-adding the same
// live domain objects to a long-lived context's change tracker across
// repeated saves would throw "already tracked" on the second call.
public sealed class ThingRepository(IDbContextFactory<GameDbContext> dbContextFactory) : IThingRepository
{
    public async Task<Thing?> LoadTreeAsync(ThingId rootId, CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var all = await ReconstructAllAsync(context, ct);
        return all.GetValueOrDefault(rootId.Value);
    }

    public async Task<Thing?> FindPlayerByNameAsync(string name, CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var all = await ReconstructAllAsync(context, ct);
        return all.Values.FirstOrDefault(t => t.HasBehavior<PlayerBehavior>() && t.Name == name);
    }

    // Deletes and reinserts every row for root + its full live subtree.
    // Simple and correct given save-on-shutdown/-disconnect call frequency
    // (not a hot path) - see docs/persistence.md Write Frequency.
    public async Task SaveTreeAsync(Thing root, CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);

        var subtree = CollectSubtree(root).ToList();
        var thingGuids = subtree.Select(t => t.Id.Value).ToList();
        var thingIds = subtree.Select(t => t.Id).ToList();

        var existingBehaviors = await context.Behaviors
            .Where(b => thingGuids.Contains(EF.Property<Guid>(b, "ThingId")))
            .ToListAsync(ct);
        context.Behaviors.RemoveRange(existingBehaviors);

        var existingThings = await context.Things
            .Where(t => thingIds.Contains(t.Id))
            .ToListAsync(ct);
        context.Things.RemoveRange(existingThings);

        await context.SaveChangesAsync(ct);

        foreach (var thing in subtree)
        {
            context.Things.Add(thing);
            context.Entry(thing).Property("ParentId").CurrentValue = thing.Parent?.Id.Value;

            foreach (var behavior in thing.Behaviors.All)
            {
                context.Behaviors.Add(behavior);
                context.Entry(behavior).Property("ThingId").CurrentValue = thing.Id.Value;

                switch (behavior)
                {
                    case ExitBehavior exit:
                        context.Entry(exit).Property("ExitDestinationId").CurrentValue = exit.Destination.Id.Value;
                        break;
                    case LockableBehavior lockable:
                        context.Entry(lockable).Property("LockableRequiredKeyId").CurrentValue =
                            lockable.RequiredKey?.Id.Value;
                        break;
                }
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private static IEnumerable<Thing> CollectSubtree(Thing root)
    {
        yield return root;
        foreach (var child in root.Children)
            foreach (var descendant in CollectSubtree(child))
                yield return descendant;
    }

    private static async Task<Dictionary<Guid, Thing>> ReconstructAllAsync(GameDbContext context, CancellationToken ct)
    {
        var thingRows = await context.Things
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                ParentId = EF.Property<Guid?>(t, "ParentId"),
            })
            .ToListAsync(ct);

        var things = new Dictionary<Guid, Thing>();
        var parentOf = new Dictionary<Guid, Guid?>();
        foreach (var row in thingRows)
        {
            things[row.Id.Value] = new Thing { Id = row.Id, Name = row.Name, Description = row.Description };
            parentOf[row.Id.Value] = row.ParentId;
        }

        var behaviors = await context.Behaviors.ToListAsync(ct);
        var exitDestinations = new List<(ExitBehavior Exit, Guid DestinationId)>();
        var lockableKeys = new List<(LockableBehavior Lockable, Guid? KeyId)>();

        foreach (var behavior in behaviors)
        {
            var thingId = (Guid)context.Entry(behavior).Property("ThingId").CurrentValue!;
            if (!things.TryGetValue(thingId, out var owner))
                continue;

            // Behaviors.Add triggers OnAddBehavior normally - this IS the
            // real domain path, not a workaround. See docs/persistence.md
            // Rehydration.
            owner.Behaviors.Add(behavior);

            switch (behavior)
            {
                case ExitBehavior exit:
                    var destId = (Guid)context.Entry(exit).Property("ExitDestinationId").CurrentValue!;
                    exitDestinations.Add((exit, destId));
                    break;
                case LockableBehavior lockable:
                    var keyId = (Guid?)context.Entry(lockable).Property("LockableRequiredKeyId").CurrentValue;
                    lockableKeys.Add((lockable, keyId));
                    break;
            }
        }

        foreach (var (exit, destId) in exitDestinations)
            if (things.TryGetValue(destId, out var destination))
                exit.Destination = destination;

        foreach (var (lockable, keyId) in lockableKeys)
            lockable.RequiredKey = keyId is { } id ? things.GetValueOrDefault(id) : null;

        // Attach via AttachLoadedChild (no AddChildEvent publish) - restoring
        // already-existing state, not a new pickup/move. See
        // docs/persistence.md Rehydration.
        foreach (var (thingId, parentId) in parentOf)
        {
            if (parentId is { } pid && things.TryGetValue(pid, out var parent))
                parent.AttachLoadedChild(things[thingId]);
        }

        return things;
    }
}
