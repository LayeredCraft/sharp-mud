using SharpMud.Engine.Combat;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class FleeCommand(ICombatManager combatManager, IRandomSource random) : ICommand
{
    public string Verb => "flee";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (!combatManager.TryGetEncounter(ctx.Actor.Id, out var encounter))
        {
            await ctx.Session.WriteLineAsync("You aren't fighting anything.", ct);
            return;
        }

        if (ctx.CurrentRoom.Exits.Count == 0)
        {
            await ctx.Session.WriteLineAsync("There's nowhere to flee to!", ct);
            return;
        }

        // Success chance: DEX-influenced per docs/combat.md, but the exact
        // formula is still an open item there. Flat 60% until it's defined.
        var success = random.Next(1, 100) <= 60;
        if (!success)
        {
            await ctx.Session.WriteLineAsync("You fail to escape!", ct);
            return;
        }

        var exit = ctx.CurrentRoom.Exits[random.Next(0, ctx.CurrentRoom.Exits.Count - 1)];
        var destination = ctx.World.GetRoom(exit.DestinationRoomId);
        if (destination is null)
        {
            await ctx.Session.WriteLineAsync("You fail to escape!", ct);
            return;
        }

        combatManager.EndEncounter(ctx.Actor.Id);
        await ctx.Session.WriteLineAsync($"You flee {exit.Direction.ToDisplayString()}!", ct);

        await ctx.World.MovePlayerAsync(ctx.Actor, ctx.CurrentRoom, destination, exit.Direction, ct);
        await LookCommand.SendRoomDescriptionAsync(ctx.Actor, destination, ctx.World, ctx.Session, ct);
    }
}
