using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

public sealed class FleeCommand : ICommand
{
    private readonly ICombatManager _combatManager;
    private readonly IDiceRoller _dice;
    private readonly IRandomSource _random;

    public FleeCommand(ICombatManager combatManager, IDiceRoller dice, IRandomSource random)
    {
        _combatManager = combatManager;
        _dice = dice;
        _random = random;
    }

    public string Verb => "flee";
    public IReadOnlyList<string> Aliases { get; } = [];

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
