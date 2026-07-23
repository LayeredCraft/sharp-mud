using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

/// <summary>
/// The <c>flee</c> command - attempts to escape an active combat encounter
/// through a random exit. Registered by <c>AddSharpMudRpgRuleset(...)</c>,
/// not meant to be constructed directly by a consumer.
/// </summary>
public sealed class FleeCommand : ICommand
{
    private readonly ICombatManager _combatManager;
    private readonly IDiceRoller _dice;
    private readonly IRandomSource _random;

    /// <summary>Creates the command against the shared <see cref="ICombatManager"/> and dice/randomness sources.</summary>
    public FleeCommand(ICombatManager combatManager, IDiceRoller dice, IRandomSource random)
    {
        _combatManager = combatManager;
        _dice = dice;
        _random = random;
    }

    /// <summary>The canonical verb, <c>flee</c>. No aliases.</summary>
    public string Verb => "flee";

    /// <summary>No aliases for <see cref="Verb"/>.</summary>
    public IReadOnlyList<string> Aliases { get; } = [];

    /// <summary>
    /// Guards (an active encounter exists, the current room has an exit),
    /// rolls a flat success chance, and on success publishes the same <see
    /// cref="UseExitEvent"/> request <c>MoveCommand</c> does (so a locked
    /// exit can still veto) before ending the encounter and moving the
    /// actor through the chosen exit.
    /// </summary>
    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (!_combatManager.TryGetEncounter(ctx.Actor.Id, out _))
        {
            await ctx.Session.WriteLineAsync("You aren't fighting anything.", ct);
            return;
        }

        var exits = ctx.CurrentRoom.Children.Select(c => c.FindBehavior<ExitBehavior>()).OfType<ExitBehavior>().ToList();
        if (exits.Count == 0)
        {
            await ctx.Session.WriteLineAsync("There's nowhere to flee to!", ct);
            return;
        }

        // Success chance: DEX-influenced per docs/combat.md, but the exact
        // formula is still an open item there. Flat 60% until it's defined.
        var success = _dice.Roll(1, 100) <= 60;
        if (!success)
        {
            await ctx.Session.WriteLineAsync("You fail to escape!", ct);
            return;
        }

        var exit = exits[_random.Next(0, exits.Count - 1)];

        // Same request/cancellation path MoveCommand uses - without this,
        // a locked exit blocks a normal move but not a flee through the
        // exact same exit.
        var exitThing = exit.Parent!;
        var request = new UseExitEvent { ActiveThing = ctx.Actor, Exit = exitThing };
        exitThing.Events.PublishRequest(request, EventScope.SelfOnly);
        if (request.IsCanceled)
        {
            await ctx.Session.WriteLineAsync(request.CancelReason ?? "You can't escape that way!", ct);
            return;
        }

        var destination = exit.Destination;

        // Remove can itself publish a cancellable RemoveChildEvent and
        // return false (e.g. a future "can't leave this room" behavior) -
        // same check MoveCommand does, and for the same reason: proceeding
        // to Add into destination after a failed Remove would leave the
        // actor added to the new room without ever having left the old
        // one. EndEncounter/messaging only happen once the move is real.
        if (!ctx.CurrentRoom.Remove(ctx.Actor))
        {
            await ctx.Session.WriteLineAsync("You can't escape that way!", ct);
            return;
        }

        // Add can itself publish a cancellable AddChildEvent and return
        // false (e.g. a future "room is full" behavior). Unlike Remove
        // above, there's no early-return option here - the actor is already
        // detached from ctx.CurrentRoom - so a failed Add is rolled back by
        // re-adding to the original room, keeping the encounter (and the
        // actor) exactly where they were rather than leaving them parentless
        // after an announced-but-not-actually-completed flee.
        if (!destination.Add(ctx.Actor))
        {
            ctx.CurrentRoom.Add(ctx.Actor);
            await ctx.Session.WriteLineAsync("You can't escape that way!", ct);
            return;
        }

        _combatManager.EndEncounter(ctx.Actor.Id);
        await ctx.Session.WriteLineAsync($"You flee {exit.Direction.ToDisplayString()}!", ct);

        await RoomBroadcast.ToOccupantsAsync(
            ctx.CurrentRoom, ctx.Actor, $"{ctx.Actor.Name} flees {exit.Direction.ToDisplayString()}.", ct);

        await LookCommand.SendRoomDescriptionAsync(ctx.Actor, destination, ct);
    }
}
