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
    /// rolls a flat success chance, and on success ends the encounter and
    /// moves the actor through a random exit.
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
        var destination = exit.Destination;

        _combatManager.EndEncounter(ctx.Actor.Id);
        await ctx.Session.WriteLineAsync($"You flee {exit.Direction.ToDisplayString()}!", ct);

        await RoomBroadcast.ToOccupantsAsync(
            ctx.CurrentRoom, ctx.Actor, $"{ctx.Actor.Name} flees {exit.Direction.ToDisplayString()}.", ct);

        ctx.CurrentRoom.Remove(ctx.Actor);
        destination.Add(ctx.Actor);

        await LookCommand.SendRoomDescriptionAsync(ctx.Actor, destination, ct);
    }
}
